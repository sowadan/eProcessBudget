using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>
/// Requêtes partagées par les services des axes. Les montants viennent toujours de
/// <see cref="SituationService.Calculer"/> : mêmes conventions que le cycle commun (pièces
/// validées seulement, réalisation = paiements).
/// </summary>
internal static class RequetesAxes
{
    /// <summary>Dotations d'un exercice imputées sur une ligne de l'axe.</summary>
    public static IQueryable<Dotation> DotationsAxe(AppDbContext ctx, Ulid axeId, Ulid exerciceId)
        => ctx.Dotations.AsNoTracking()
            .Where(d => d.ExerciceId == exerciceId && d.LigneBudgetaire.SousRubrique.Rubrique.AxeId == axeId);

    /// <summary>Situation cumulée des dotations de chaque projet sur l'exercice.</summary>
    public static async Task<Dictionary<Ulid, MontantsDotation>> MontantsParProjetAsync(AppDbContext ctx, Ulid exerciceId, IReadOnlyCollection<Ulid> projetIds)
    {
        if (projetIds.Count == 0) return [];
        var lignes = await SituationService.Calculer(ctx, ctx.Dotations.AsNoTracking()
                .Where(d => d.ExerciceId == exerciceId && d.ProjetId != null && projetIds.Contains(d.ProjetId.Value)))
            .ToListAsync();
        return lignes.GroupBy(l => l.ProjetId!.Value)
                     .ToDictionary(g => g.Key, g => MontantsDotation.Somme(g.Select(l => l.Montants)));
    }

    /// <summary>Kilomètres entretenus cumulés de chaque projet, toutes périodes confondues.</summary>
    public static async Task<Dictionary<Ulid, decimal>> KmRealisesAsync(AppDbContext ctx, IReadOnlyCollection<Ulid> projetIds)
    {
        if (projetIds.Count == 0) return [];
        return await ctx.AvancementsPhysiques.AsNoTracking()
            .Where(a => projetIds.Contains(a.ProjetId))
            .GroupBy(a => a.ProjetId)
            .Select(g => new { g.Key, Km = g.Sum(a => a.KmRealises ?? 0) })
            .ToDictionaryAsync(x => x.Key, x => x.Km);
    }

    /// <summary>
    /// Montants engagé (validé) et payé de chaque contrat, tous exercices confondus. Les
    /// encours reportés sont exclus de l'engagé : ils reprennent un engagement déjà compté.
    /// </summary>
    public static async Task<Dictionary<Ulid, (decimal Engage, decimal Paye)>> ExecutionParContratAsync(AppDbContext ctx, IReadOnlyCollection<Ulid> contratIds)
    {
        if (contratIds.Count == 0) return [];
        var engage = await ctx.Engagements.AsNoTracking()
            .Where(e => e.ContratId != null && contratIds.Contains(e.ContratId.Value) && e.Statut == StatutSaisie.Valide && !e.EstReport)
            .GroupBy(e => e.ContratId!.Value)
            .Select(g => new { g.Key, Montant = g.Sum(e => e.Montant) })
            .ToDictionaryAsync(x => x.Key, x => x.Montant);
        var paye = await ctx.Paiements.AsNoTracking()
            .Where(p => p.Statut == StatutSaisie.Valide && p.Engagement.Statut == StatutSaisie.Valide
                     && p.Engagement.ContratId != null && contratIds.Contains(p.Engagement.ContratId.Value))
            .GroupBy(p => p.Engagement.ContratId!.Value)
            .Select(g => new { g.Key, Montant = g.Sum(p => p.Montant) })
            .ToDictionaryAsync(x => x.Key, x => x.Montant);
        return contratIds.ToDictionary(id => id, id => (engage.GetValueOrDefault(id), paye.GetValueOrDefault(id)));
    }

    /// <summary>« code — libellé » des éléments d'imputation, pour un affichage.</summary>
    public static string Joindre(params string?[] elements) => string.Join(" · ", elements.Where(e => !string.IsNullOrEmpty(e)));
}
