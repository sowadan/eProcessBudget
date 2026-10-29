using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.BudgetProgramme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

public sealed record LigneCycle(
    Ulid Id, string Libelle, int AnneeReference, int AnneeBase, int AnneeFin, StatutCycle Statut, string? CyclePrecedent);

/// <summary>
/// Cycles de programmation triennale glissante (cahier fonctionnel § 3.2.2) : année de
/// référence N-1, année de base N, projections N+1 et N+2.
///
/// Brouillon → Validé → Archivé. La validation exige que chaque indicateur ait une valeur
/// de référence et une cible pour chaque année du cycle (règle § 3.4), et un cadre logique
/// complet (règle § 4.2). La reconduction crée le cycle suivant (N+1), en brouillon, avec
/// les projections du cycle reconduit (ReconductionCycle).
/// </summary>
public interface ICycleProgrammationService
{
    Task<List<LigneCycle>> ListerAsync();
    Task<(bool Ok, string? Erreur)> CreerAsync(int anneeBase, string? libelle);
    Task<(bool Ok, string? Erreur)> RenommerAsync(Ulid cycleId, string? libelle);
    Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid cycleId);
    Task<(bool Ok, string? Erreur)> ArchiverAsync(Ulid cycleId);
    Task<(bool Ok, string? Erreur)> ReconduireAsync(Ulid cycleId);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid cycleId);
}

public sealed class CycleProgrammationService : ICycleProgrammationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IAuditService _audit;

    public CycleProgrammationService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<LigneCycle>> ListerAsync()
    {
        if (await _acces.MembreOrganismeAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.CyclesProgrammation.AsNoTracking()
            .OrderByDescending(c => c.AnneeBase)
            .Select(c => new LigneCycle(c.Id, c.Libelle, c.AnneeReference, c.AnneeBase, c.AnneeFin, c.Statut,
                c.CyclePrecedent == null ? null : c.CyclePrecedent.Libelle))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> CreerAsync(int anneeBase, string? libelle)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        return await AjouterAsync(ctx, acteurId, anneeBase, libelle, null);
    }

    public async Task<(bool Ok, string? Erreur)> RenommerAsync(Ulid cycleId, string? libelle)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        libelle = libelle?.Trim();
        if (string.IsNullOrEmpty(libelle) || libelle.Length > 100) return (false, "Le libellé est obligatoire (100 caractères au plus).");

        await using var ctx = await _factory.CreateDbContextAsync();
        var cycle = await ctx.CyclesProgrammation.FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null) return (false, "Cycle introuvable.");

        cycle.Libelle = libelle;
        cycle.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid cycleId)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var cycle = await ctx.CyclesProgrammation.FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null) return (false, "Cycle introuvable.");
        if (cycle.Statut != StatutCycle.Brouillon) return (false, "Seul un cycle en brouillon peut être validé.");

        // Règle § 3.4 : référence et cible de chaque année du cycle, pour chaque indicateur.
        var nbAnnees = cycle.AnneeFin - cycle.AnneeBase + 1;
        var incomplets = await ctx.Indicateurs.AsNoTracking()
            .Where(i => i.ValeurReference == null || i.AnneeReference == null
                     || i.Cibles.Where(c => c.CycleProgrammationId == cycleId
                                         && c.Annee >= cycle.AnneeBase && c.Annee <= cycle.AnneeFin)
                                .Select(c => c.Annee).Distinct().Count() < nbAnnees)
            .OrderBy(i => i.Code)
            .Select(i => i.Code)
            .Take(10)
            .ToListAsync();

        if (incomplets.Count > 0)
            return (false, $"Validation impossible : référence ou cibles {cycle.AnneeBase}-{cycle.AnneeFin} manquantes " +
                           $"pour les indicateurs {string.Join(", ", incomplets)}{(incomplets.Count == 10 ? "…" : "")}.");

        // Règle § 4.2 : chaque objectif spécifique du cadre logique a un indicateur et une source de vérification.
        var anomalies = await CadreLogiqueService.AnomaliesAsync(ctx, null, cycleId);
        if (anomalies.Count > 0)
            return (false, $"Validation impossible : cadre logique incomplet ({string.Join(" ; ", anomalies.Take(5))}{(anomalies.Count > 5 ? "…" : "")}).");

        cycle.Statut = StatutCycle.Valide;
        cycle.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "VALIDATION", AccesParametrage.ModuleAudit, nameof(CycleProgrammation),
            cycle.Id.ToString(), null, new { cycle.Libelle, cycle.AnneeBase });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ArchiverAsync(Ulid cycleId)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var cycle = await ctx.CyclesProgrammation.FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null) return (false, "Cycle introuvable.");
        if (cycle.Statut != StatutCycle.Valide) return (false, "Seul un cycle validé peut être archivé.");

        cycle.Statut = StatutCycle.Archive;
        cycle.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "ARCHIVAGE", AccesParametrage.ModuleAudit, nameof(CycleProgrammation),
            cycle.Id.ToString(), null, new { cycle.Libelle });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ReconduireAsync(Ulid cycleId)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var cycle = await ctx.CyclesProgrammation.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null) return (false, "Cycle introuvable.");
        if (cycle.Statut == StatutCycle.Brouillon) return (false, "Validez le cycle avant de le reconduire.");

        return await AjouterAsync(ctx, acteurId, cycle.AnneeBase + 1, null, cycle.Id);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid cycleId)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var cycle = await ctx.CyclesProgrammation.FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null) return (false, "Cycle introuvable.");
        if (cycle.Statut != StatutCycle.Brouillon) return (false, "Seul un cycle en brouillon peut être supprimé.");

        var utilisation = await VerificateurUtilisation.TrouverAsync(ctx, typeof(CycleProgrammation), cycle.Id, false);
        if (utilisation is not null) return (false, $"Suppression impossible : le cycle est utilisé par {utilisation}.");

        cycle.EstSupprime = true;
        cycle.DateSuppression = DateTime.UtcNow;
        cycle.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION", AccesParametrage.ModuleAudit, nameof(CycleProgrammation),
            cycle.Id.ToString(), new { cycle.Libelle }, null);
        return (true, null);
    }

    private async Task<(bool, string?)> AjouterAsync(AppDbContext ctx, Ulid acteurId, int anneeBase, string? libelle, Ulid? precedentId)
    {
        if (anneeBase is < 2000 or > 2100) return (false, "L'année de base doit être comprise entre 2000 et 2100.");
        if (await ctx.CyclesProgrammation.AnyAsync(c => c.AnneeBase == anneeBase))
            return (false, $"Un cycle a déjà {anneeBase} pour année de base.");

        libelle = string.IsNullOrWhiteSpace(libelle) ? $"Programmation {anneeBase}-{anneeBase + 2}" : libelle.Trim();
        if (libelle.Length > 100) return (false, "Le libellé ne doit pas dépasser 100 caractères.");

        var cycle = new CycleProgrammation
        {
            Libelle = libelle,
            AnneeReference = anneeBase - 1,   // contrainte CHECK en base
            AnneeBase = anneeBase,
            AnneeFin = anneeBase + 2,
            Statut = StatutCycle.Brouillon,
            CyclePrecedentId = precedentId,
            CreePar = acteurId
        };
        ctx.CyclesProgrammation.Add(cycle);

        // Reconduction glissante : les projections du cycle précédent servent de base de travail.
        if (precedentId is { } idPrecedent)
        {
            var precedent = await ctx.CyclesProgrammation.AsNoTracking().FirstAsync(c => c.Id == idPrecedent);
            await ReconductionCycle.CopierAsync(ctx, precedent, cycle, acteurId);
        }

        // L'exercice de l'année de base, s'il existe déjà, est rattaché au cycle.
        var exercice = await ctx.Exercices.FirstOrDefaultAsync(e => e.Annee == anneeBase && e.CycleProgrammationId == null);
        if (exercice is not null) exercice.CycleProgrammation = cycle;

        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, precedentId is null ? "CREATION" : "RECONDUCTION", AccesParametrage.ModuleAudit,
            nameof(CycleProgrammation), cycle.Id.ToString(), null, new { cycle.Libelle, cycle.AnneeBase });
        return (true, null);
    }
}
