using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eProcessBudget.Infrastructure.Services.Recettes;

/// <summary>
/// Alerte sur les échéances de recouvrement dépassées (cahier technique § 2.9) : une
/// échéance dont la date est passée et que les encaissements ne couvrent pas est signalée
/// aux détenteurs de PrevisionRecetteSaisir et EncaissementSaisir, une seule fois.
/// </summary>
public static class AlerteRecouvrement
{
    /// <summary>Clé de la notification d'une échéance : sert aussi à ne pas notifier deux fois.</summary>
    internal static string Cle(Ulid echeanceId) => $"Echeance:{echeanceId}:Retard";

    /// <summary>Notifie les échéances en retard des exercices ouverts de l'organisme du contexte. Renvoie le nombre d'alertes.</summary>
    internal static async Task<int> VerifierAsync(AppDbContext ctx, IDroitService droits, INotificationService notifications, DateOnly aujourdhui)
    {
        var enRetard = (await SyntheseRecetteService.SuivreEcheancesAsync(ctx,
                ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.Exercice.Statut == StatutExercice.Ouvert), aujourdhui))
            .Where(e => e.EnRetard).ToList();
        if (enRetard.Count == 0) return 0;

        var cles = enRetard.Select(e => Cle(e.EcheanceId)).ToList();
        var dejaEmises = (await ctx.Notifications.IgnoreQueryFilters()
            .Where(n => n.CleEntite != null && cles.Contains(n.CleEntite))
            .Select(n => n.CleEntite!).Distinct().ToListAsync()).ToHashSet();
        enRetard = enRetard.Where(e => !dejaEmises.Contains(Cle(e.EcheanceId))).ToList();
        if (enRetard.Count == 0) return 0;

        var destinataires = await DetenteursDroit.ListerAsync(ctx, droits, ctx.OrganismeCourantId, AccesRecettes.Gestionnaires);
        if (destinataires.Count == 0) return 0;

        foreach (var e in enRetard)
            await notifications.NotifierGroupeAsync(TypeNotification.EcheanceRecouvrement,
                $"L'échéance du {e.Date:dd/MM/yyyy} ({e.Prevision}) n'est pas couverte : {e.Reste:N2} restent à recouvrer sur {e.Attendu:N2}.",
                destinataires, titre: "Échéance de recouvrement dépassée", lien: "/recettes/echeancier", cleEntite: Cle(e.EcheanceId));
        return enRetard.Count;
    }
}

/// <summary>Contrôle nocturne des échéances de recouvrement (cahier technique § 2.9), organismes actifs.</summary>
public sealed class TraitementRecouvrementNocturne : BackgroundService
{
    /// <summary>Heure locale du passage quotidien, après les seuils (2 h) et les appuis (2 h 30).</summary>
    private static readonly TimeOnly Heure = new(3, 0);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TraitementRecouvrementNocturne> _logger;

    public TraitementRecouvrementNocturne(IServiceScopeFactory scopes, ILogger<TraitementRecouvrementNocturne> logger)
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
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Contrôle nocturne des échéances de recouvrement en échec."); }
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
            total += await AlerteRecouvrement.VerifierAsync(ctx, droits, notifications, DateOnly.FromDateTime(DateTime.Today));
        }
        _logger.LogInformation("Contrôle nocturne des échéances : {Alertes} alerte(s) pour {Organismes} organisme(s).", total, organismes.Count);
    }
}
