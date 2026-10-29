using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eProcessBudget.Infrastructure.Services.Audit;

/// <summary>Lignes supprimées par une purge.</summary>
public sealed record ResultatPurge(int LignesAudit, int LignesConnexion)
{
    public int Total => LignesAudit + LignesConnexion;
}

/// <summary>
/// Purge des journaux d'audit et de connexion au-delà de la durée de rétention de l'organisme
/// (cahier fonctionnel § 3.13.2, technique § 2.7) : 12 mois au minimum. C'est la seule
/// suppression admise sur ces journaux en ajout seul ; elle est elle-même journalisée.
/// </summary>
public static class PurgeJournaux
{
    public const int RetentionMinimaleMois = 12;

    /// <summary>Supprime les lignes de l'organisme (null : niveau plateforme) antérieures à la rétention.</summary>
    internal static async Task<ResultatPurge> PurgerAsync(AppDbContext ctx, Ulid? organismeId, int retentionMois, DateTime maintenant, CancellationToken jeton = default)
    {
        var limite = maintenant.AddMonths(-Math.Max(RetentionMinimaleMois, retentionMois));
        var audits = ctx.AuditLogs.Where(a => a.OrganismeId == organismeId && a.DateAction < limite);
        var connexions = ctx.JournalConnexions.Where(j => j.OrganismeId == organismeId && j.DateHeure < limite);

        ResultatPurge resultat;
        if (ctx.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Fournisseur de test : pas de suppression ensembliste.
            var a = await audits.ToListAsync(jeton);
            var c = await connexions.ToListAsync(jeton);
            ctx.PurgeJournauxEnCours = true;
            try
            {
                ctx.AuditLogs.RemoveRange(a);
                ctx.JournalConnexions.RemoveRange(c);
                await ctx.SaveChangesAsync(jeton);
            }
            finally { ctx.PurgeJournauxEnCours = false; }
            resultat = new(a.Count, c.Count);
        }
        else
        {
            resultat = new(await audits.ExecuteDeleteAsync(jeton), await connexions.ExecuteDeleteAsync(jeton));
        }

        if (resultat.Total > 0)
        {
            ctx.AuditLogs.Add(new AuditLog
            {
                Id = Ulid.NewUlid(), OrganismeId = organismeId, Action = "PURGE", Module = "Audit", Entite = "Journaux",
                NouvelleValeur = System.Text.Json.JsonSerializer.Serialize(new { resultat.LignesAudit, resultat.LignesConnexion, Avant = limite.ToString("yyyy-MM-dd"), RetentionMois = Math.Max(RetentionMinimaleMois, retentionMois) }),
                Commentaire = "Purge planifiée au-delà de la durée de rétention.", DateAction = maintenant, EstSucces = true
            });
            await ctx.SaveChangesAsync(jeton);
        }
        return resultat;
    }

    /// <summary>Purge tous les organismes (selon leur rétention) et le niveau plateforme (rétention minimale).</summary>
    internal static async Task<ResultatPurge> PurgerToutAsync(IDbContextFactory<AppDbContext> fabrique, DateTime maintenant, CancellationToken jeton = default)
    {
        List<Ulid> organismes;
        await using (var ctx = await fabrique.CreateDbContextAsync(jeton))
            organismes = await ctx.Organismes.AsNoTracking().Select(o => o.Id).ToListAsync(jeton);

        int audits = 0, connexions = 0;
        foreach (var organismeId in organismes)
        {
            jeton.ThrowIfCancellationRequested();
            await using var ctx = await fabrique.CreateDbContextAsync(jeton);
            ctx.OrganismeCourantId = organismeId;
            var retention = await ctx.ParametresOrganisme.AsNoTracking().Select(p => (int?)p.RetentionJournauxMois).FirstOrDefaultAsync(jeton) ?? RetentionMinimaleMois;
            var r = await PurgerAsync(ctx, organismeId, retention, maintenant, jeton);
            audits += r.LignesAudit;
            connexions += r.LignesConnexion;
        }
        await using (var ctx = await fabrique.CreateDbContextAsync(jeton))
        {
            var r = await PurgerAsync(ctx, null, RetentionMinimaleMois, maintenant, jeton);
            audits += r.LignesAudit;
            connexions += r.LignesConnexion;
        }
        return new(audits, connexions);
    }
}

/// <summary>Purge hebdomadaire des journaux (cahier technique § 2.9) : chaque dimanche à 4 h.</summary>
public sealed class TraitementPurgeJournaux : BackgroundService
{
    private static readonly TimeOnly Heure = new(4, 0);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TraitementPurgeJournaux> _logger;

    public TraitementPurgeJournaux(IServiceScopeFactory scopes, ILogger<TraitementPurgeJournaux> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var maintenant = DateTime.Now;
            var prochain = maintenant.Date.AddDays(((int)DayOfWeek.Sunday - (int)maintenant.DayOfWeek + 7) % 7).Add(Heure.ToTimeSpan());
            if (prochain <= maintenant) prochain = prochain.AddDays(7);
            try { await Task.Delay(prochain - maintenant, stoppingToken); }
            catch (OperationCanceledException) { return; }

            try
            {
                using var scope = _scopes.CreateScope();
                var fabrique = scope.ServiceProvider.GetRequiredKeyedService<IDbContextFactory<AppDbContext>>(FabriqueContexteOrganisme.CleSansOrganisme);
                var r = await PurgeJournaux.PurgerToutAsync(fabrique, DateTime.UtcNow, stoppingToken);
                _logger.LogInformation("Purge des journaux : {Audit} ligne(s) d'audit et {Connexions} ligne(s) de connexion supprimées.", r.LignesAudit, r.LignesConnexion);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { _logger.LogError(ex, "Purge des journaux en échec."); }
        }
    }
}
