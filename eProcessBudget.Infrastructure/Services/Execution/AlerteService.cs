using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>
/// Alertes de consommation (cahier fonctionnel § 4.2, technique § 2.9) : quand le taux
/// d'engagement d'une dotation (y compris reports) atteint un seuil paramétré, le
/// Responsable Budget est notifié, une fois par dotation et par seuil.
///
/// Seuils applicables : ceux de la ligne s'il y en a, sinon ceux de l'axe, sinon les
/// seuils globaux (80 / 90 / 100 % par défaut).
/// </summary>
public interface IAlerteService
{
    /// <summary>Contrôle les dotations après une validation (engagement, ajustement, dépassement).</summary>
    Task VerifierAsync(IReadOnlyCollection<Ulid> dotationIds);
}

public sealed class AlerteService : IAlerteService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IDroitService _droits;
    private readonly INotificationService _notifications;
    private readonly ILogger<AlerteService> _logger;

    public AlerteService(IDbContextFactory<AppDbContext> factory, IDroitService droits, INotificationService notifications, ILogger<AlerteService> logger)
    {
        _factory = factory;
        _droits = droits;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task VerifierAsync(IReadOnlyCollection<Ulid> dotationIds)
    {
        if (dotationIds.Count == 0) return;
        try
        {
            await using var ctx = await _factory.CreateDbContextAsync();
            await VerifierAsync(ctx, ctx.Dotations.AsNoTracking().Where(d => dotationIds.Contains(d.Id)), _droits, _notifications);
        }
        catch (Exception ex)
        {
            // Comme l'audit, l'alerte ne fait jamais échouer l'opération qui la déclenche.
            _logger.LogError(ex, "Contrôle des seuils d'alerte impossible.");
        }
    }

    /// <summary>Clé de la notification d'un seuil : sert aussi à ne pas notifier deux fois.</summary>
    internal static string Cle(Ulid dotationId, decimal seuil) => $"Dotation:{dotationId}:Seuil:{seuil:0.####}";

    /// <summary>Un seuil paramétré, tel que lu en base.</summary>
    internal sealed record SeuilLu(PorteeSeuil Portee, Ulid? AxeId, Ulid? LigneBudgetaireId, decimal Pourcentage);

    internal static Task<List<SeuilLu>> LireSeuilsAsync(AppDbContext ctx)
        => ctx.SeuilsAlerte.AsNoTracking().Select(s => new SeuilLu(s.Portee, s.AxeId, s.LigneBudgetaireId, s.Pourcentage)).ToListAsync();

    /// <summary>Seuils d'une dotation : ceux de sa ligne, sinon de son axe, sinon les seuils globaux.</summary>
    internal static List<decimal> SeuilsApplicables(IReadOnlyList<SeuilLu> seuils, Ulid ligneBudgetaireId, Ulid axeId)
    {
        var propres = seuils.Where(s => s.Portee == PorteeSeuil.LigneBudgetaire && s.LigneBudgetaireId == ligneBudgetaireId).ToList();
        if (propres.Count == 0) propres = seuils.Where(s => s.Portee == PorteeSeuil.Axe && s.AxeId == axeId).ToList();
        if (propres.Count == 0) propres = seuils.Where(s => s.Portee == PorteeSeuil.Globale).ToList();
        return propres.Select(s => s.Pourcentage).ToList();
    }

    /// <summary>Contrôle les dotations et notifie chaque seuil nouvellement atteint. Renvoie le nombre d'alertes émises.</summary>
    internal static async Task<int> VerifierAsync(AppDbContext ctx, IQueryable<Dotation> dotations, IDroitService droits, INotificationService notifications)
    {
        var seuils = await LireSeuilsAsync(ctx);
        if (seuils.Count == 0) return 0;

        var alertes = new List<(DotationCalculee Dotation, decimal Seuil, decimal Taux)>();
        foreach (var d in await SituationService.Calculer(ctx, dotations).ToListAsync())
        {
            var taux = d.Montants.TauxEngagementYCReports;
            if (CalculsExecution.SeuilAtteint(taux, SeuilsApplicables(seuils, d.LigneBudgetaireId, d.AxeId)) is { } seuil)
                alertes.Add((d, seuil, taux!.Value));
        }
        if (alertes.Count == 0) return 0;

        var cles = alertes.Select(a => Cle(a.Dotation.Id, a.Seuil)).ToList();
        var dejaEmises = (await ctx.Notifications.IgnoreQueryFilters()
            .Where(n => n.CleEntite != null && cles.Contains(n.CleEntite))
            .Select(n => n.CleEntite!).Distinct().ToListAsync()).ToHashSet();
        alertes = alertes.Where(a => !dejaEmises.Contains(Cle(a.Dotation.Id, a.Seuil))).ToList();
        if (alertes.Count == 0) return 0;

        var destinataires = await DetenteursDroit.ListerAsync(ctx, droits, ctx.OrganismeCourantId, AccesExecution.ResponsablesBudget);
        if (destinataires.Count == 0) return 0;

        foreach (var (d, seuil, taux) in alertes)
        {
            var imputation = d.VersLigne().Imputation;
            await notifications.NotifierGroupeAsync(TypeNotification.SeuilConsommation,
                $"La ligne {d.LigneCode} — {d.LigneLibelle}{(imputation.Length > 0 ? " (" + imputation + ")" : "")} est engagée à {taux:0.##} % "
                + $"de sa dotation totale (seuil de {seuil:0.##} %).",
                destinataires, titre: $"Seuil de {seuil:0.##} % atteint", lien: $"/execution/dotations/{d.Id}", cleEntite: Cle(d.Id, seuil));
        }
        return alertes.Count;
    }
}

/// <summary>
/// Contrôle nocturne des seuils (cahier technique § 2.9) : chaque nuit, pour chaque
/// organisme actif, les dotations des exercices ouverts. Rattrape ce que les contrôles
/// au fil de l'eau n'ont pas vu (seuils modifiés, reports reçus...).
/// </summary>
public sealed class TraitementAlertesNocturne : BackgroundService
{
    /// <summary>Heure locale du passage quotidien.</summary>
    private static readonly TimeOnly Heure = new(2, 0);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TraitementAlertesNocturne> _logger;

    public TraitementAlertesNocturne(IServiceScopeFactory scopes, ILogger<TraitementAlertesNocturne> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var maintenant = DateTime.Now;
            var prochain = maintenant.Date.Add(Heure.ToTimeSpan());
            if (prochain <= maintenant) prochain = prochain.AddDays(1);
            try { await Task.Delay(prochain - maintenant, stoppingToken); }
            catch (OperationCanceledException) { return; }

            try { await ExecuterAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Contrôle nocturne des seuils en échec."); }
        }
    }

    private async Task ExecuterAsync(CancellationToken jeton)
    {
        using var scope = _scopes.CreateScope();
        var fabrique = scope.ServiceProvider.GetRequiredKeyedService<IDbContextFactory<AppDbContext>>(FabriqueContexteOrganisme.CleSansOrganisme);
        var droits = scope.ServiceProvider.GetRequiredService<IDroitService>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        List<Ulid> organismes;
        await using (var ctx = await fabrique.CreateDbContextAsync(jeton))
            organismes = await ctx.Organismes.AsNoTracking().Where(o => o.Statut == StatutOrganisme.Actif).Select(o => o.Id).ToListAsync(jeton);

        var total = 0;
        foreach (var organismeId in organismes)
        {
            jeton.ThrowIfCancellationRequested();
            await using var ctx = await fabrique.CreateDbContextAsync(jeton);
            ctx.OrganismeCourantId = organismeId;
            total += await AlerteService.VerifierAsync(ctx,
                ctx.Dotations.AsNoTracking().Where(d => d.Exercice.Statut == StatutExercice.Ouvert), droits, notifications);
        }
        _logger.LogInformation("Contrôle nocturne des seuils : {Alertes} alerte(s) pour {Organismes} organisme(s).", total, organismes.Count);
    }
}
