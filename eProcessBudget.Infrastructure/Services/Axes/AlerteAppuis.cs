using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>
/// Alerte sur les appuis en attente de versement (cahier fonctionnel § 3.8.2, technique
/// § 2.9) : un engagement d'appui validé depuis plus de
/// <see cref="CalculsAxes.DelaiAppuiEnAttenteJours"/> jours et pas entièrement versé est
/// signalé aux comptables (PaiementSaisir) et aux gestionnaires des appuis (AppuisSaisir),
/// au plus une fois par engagement et par mois tant qu'il reste à verser.
/// </summary>
public static class AlerteAppuis
{
    /// <summary>Destinataires : qui verse et qui suit les appuis.</summary>
    public static readonly TypeDroit[] Destinataires = [TypeDroit.PaiementSaisir, TypeDroit.AppuisSaisir];

    /// <summary>Clé de la notification : un rappel par engagement et par mois.</summary>
    internal static string Cle(Ulid engagementId, DateTime maintenant) => $"Appui:{engagementId}:EnAttente:{maintenant:yyyyMM}";

    /// <summary>Notifie les appuis en attente de l'organisme du contexte. Renvoie le nombre d'alertes émises.</summary>
    internal static async Task<int> VerifierAsync(AppDbContext ctx, IDroitService droits, INotificationService notifications, DateTime maintenant)
    {
        if (await AccesAxes.AxeIdAsync(ctx, AxesDepense.Appuis) is not { } axeId) return 0;
        var exercices = await ctx.Exercices.AsNoTracking().Where(e => e.Statut == StatutExercice.Ouvert).Select(e => e.Id).ToListAsync();

        var limite = maintenant.AddDays(-CalculsAxes.DelaiAppuiEnAttenteJours);
        var appuis = new List<LigneAppuiEnAttente>();
        foreach (var exerciceId in exercices)
            appuis.AddRange((await SyntheseAxeService.EnAttenteAsync(ctx, axeId, exerciceId))
                .Select(a => a.Ligne).Where(a => a.DateValidation is { } v && v <= limite));
        if (appuis.Count == 0) return 0;

        var cles = appuis.Select(a => Cle(a.EngagementId, maintenant)).ToList();
        var dejaEmises = (await ctx.Notifications.IgnoreQueryFilters()
            .Where(n => n.CleEntite != null && cles.Contains(n.CleEntite))
            .Select(n => n.CleEntite!).Distinct().ToListAsync()).ToHashSet();
        appuis = appuis.Where(a => !dejaEmises.Contains(Cle(a.EngagementId, maintenant))).ToList();
        if (appuis.Count == 0) return 0;

        var destinataires = await DetenteursDroit.ListerAsync(ctx, droits, ctx.OrganismeCourantId, Destinataires);
        if (destinataires.Count == 0) return 0;

        foreach (var a in appuis)
        {
            await notifications.NotifierGroupeAsync(TypeNotification.AppuiEnAttente,
                $"L'appui {a.Numero} — {a.Objet}{(a.Entite is null ? "" : " (" + a.Entite + ")")} est validé depuis {a.JoursAttente(maintenant)} jours ; "
                + $"il reste {a.Reste:N2} à verser sur {a.Montant:N2}.",
                destinataires, titre: "Appui en attente de versement", lien: $"/execution/engagements/{a.EngagementId}",
                cleEntite: Cle(a.EngagementId, maintenant));
        }
        return appuis.Count;
    }
}

/// <summary>
/// Contrôle nocturne des appuis en attente de versement (cahier technique § 2.9) : chaque
/// nuit, pour chaque organisme actif, les exercices ouverts.
/// </summary>
public sealed class TraitementAppuisNocturne : BackgroundService
{
    /// <summary>Heure locale du passage quotidien, après le contrôle des seuils (2 h).</summary>
    private static readonly TimeOnly Heure = new(2, 30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TraitementAppuisNocturne> _logger;

    public TraitementAppuisNocturne(IServiceScopeFactory scopes, ILogger<TraitementAppuisNocturne> logger)
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
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Contrôle nocturne des appuis en attente en échec."); }
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
            total += await AlerteAppuis.VerifierAsync(ctx, droits, notifications, DateTime.UtcNow);
        }
        _logger.LogInformation("Contrôle nocturne des appuis : {Alertes} alerte(s) pour {Organismes} organisme(s).", total, organismes.Count);
    }
}
