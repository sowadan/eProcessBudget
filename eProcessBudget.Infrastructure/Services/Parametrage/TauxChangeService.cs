using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

public sealed record LigneTauxChange(Ulid Id, DateOnly DateEffet, decimal Taux);

/// <summary>
/// Taux de change d'une devise vers la devise de référence, datés (ressources externes,
/// cahier fonctionnel § 3.2.2). Le taux applicable à une date est le dernier en vigueur.
/// </summary>
public interface ITauxChangeService
{
    Task<List<LigneTauxChange>> ListerAsync(Ulid deviseId);
    Task<(bool Ok, string? Erreur)> AjouterAsync(Ulid deviseId, DateOnly dateEffet, decimal taux);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid tauxId);

    /// <summary>Taux en vigueur à une date (null si aucun) : 1 unité de la devise = taux unités de référence.</summary>
    Task<decimal?> TauxEnVigueurAsync(Ulid deviseId, DateOnly date);
}

public sealed class TauxChangeService : ITauxChangeService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IAuditService _audit;

    public TauxChangeService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<LigneTauxChange>> ListerAsync(Ulid deviseId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.TauxChanges.AsNoTracking()
            .Where(t => t.DeviseId == deviseId)
            .OrderByDescending(t => t.DateEffet)
            .Select(t => new LigneTauxChange(t.Id, t.DateEffet, t.Taux))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> AjouterAsync(Ulid deviseId, DateOnly dateEffet, decimal taux)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);
        if (taux <= 0 || taux >= 1_000_000_000) return (false, "Le taux doit être strictement positif.");
        if (decimal.Round(taux, 4) != taux) return (false, "Le taux compte 4 décimales au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var devise = await ctx.Devises.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deviseId);
        if (devise is null) return (false, "Devise introuvable.");
        if (devise.EstReference) return (false, "La devise de référence n'a pas de taux de change.");
        if (await ctx.TauxChanges.AnyAsync(t => t.DeviseId == deviseId && t.DateEffet == dateEffet))
            return (false, $"Un taux existe déjà au {dateEffet:dd/MM/yyyy} pour cette devise.");

        var ligne = new TauxChange { DeviseId = deviseId, DateEffet = dateEffet, Taux = taux, CreePar = acteurId };
        ctx.TauxChanges.Add(ligne);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CREATION", AccesParametrage.ModuleAudit, nameof(TauxChange),
            ligne.Id.ToString(), null, new { devise.CodeIso, DateEffet = dateEffet.ToString("yyyy-MM-dd"), taux });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid tauxId)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var ligne = await ctx.TauxChanges.FirstOrDefaultAsync(t => t.Id == tauxId);
        if (ligne is null) return (false, "Taux introuvable.");

        ligne.EstSupprime = true;
        ligne.DateSuppression = DateTime.UtcNow;
        ligne.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION", AccesParametrage.ModuleAudit, nameof(TauxChange),
            tauxId.ToString(), new { DateEffet = ligne.DateEffet.ToString("yyyy-MM-dd"), ligne.Taux }, null);
        return (true, null);
    }

    public async Task<decimal?> TauxEnVigueurAsync(Ulid deviseId, DateOnly date)
    {
        if (await _acces.MembreOrganismeAsync() == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.TauxChanges.AsNoTracking()
            .Where(t => t.DeviseId == deviseId && t.DateEffet <= date)
            .OrderByDescending(t => t.DateEffet)
            .Select(t => (decimal?)t.Taux)
            .FirstOrDefaultAsync();
    }
}
