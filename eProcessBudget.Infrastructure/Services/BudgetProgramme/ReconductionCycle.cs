using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

/// <summary>
/// Reconduction glissante (cahier fonctionnel § 3.3.5) : les projections du cycle
/// précédent servent de base de travail au nouveau, qui reste révisable (brouillon).
///
/// Ancien cycle N-1 | N | N+1 | N+2   →   nouveau cycle (N+1 de base) :
///   - AE/CP, ressources, effectifs des années communes (ancien N → nouvelle référence,
///     ancien N+1 et N+2 → nouvelles N et N+1) ; la dernière année est à saisir ;
///   - cibles des indicateurs des années communes ;
///   - cadre logique complet (éléments, indicateurs, sources, risques).
/// N'enregistre rien : appelée par CycleProgrammationService dans sa transaction.
/// </summary>
internal static class ReconductionCycle
{
    public static async Task CopierAsync(AppDbContext ctx, CycleProgrammation ancien, CycleProgrammation nouveau, Ulid auteurId)
    {
        var debut = nouveau.AnneeReference;
        var fin = Math.Min(ancien.AnneeFin, nouveau.AnneeFin);

        foreach (var p in await ctx.ProgrammationsFinancieres.AsNoTracking()
                     .Where(p => p.CycleProgrammationId == ancien.Id && p.Annee >= debut && p.Annee <= fin).ToListAsync())
            ctx.ProgrammationsFinancieres.Add(new ProgrammationFinanciere
            {
                CycleProgrammation = nouveau, Annee = p.Annee, ActionId = p.ActionId, ActiviteId = p.ActiviteId,
                NatureDepenseId = p.NatureDepenseId, SourceFinancementId = p.SourceFinancementId,
                MontantAE = p.MontantAE, MontantCP = p.MontantCP, CreePar = auteurId
            });

        foreach (var r in await ctx.ProgrammationsRessources.AsNoTracking()
                     .Where(r => r.CycleProgrammationId == ancien.Id && r.Annee >= debut && r.Annee <= fin).ToListAsync())
            ctx.ProgrammationsRessources.Add(new ProgrammationRessource
            {
                CycleProgrammation = nouveau, Annee = r.Annee, NatureRecetteId = r.NatureRecetteId,
                SourceFinancementId = r.SourceFinancementId, Montant = r.Montant, CreePar = auteurId
            });

        foreach (var e in await ctx.ProgrammationsEmplois.AsNoTracking()
                     .Where(e => e.CycleProgrammationId == ancien.Id && e.Annee >= debut && e.Annee <= fin).ToListAsync())
            ctx.ProgrammationsEmplois.Add(new ProgrammationEmploi
            {
                CycleProgrammation = nouveau, Annee = e.Annee, ProgrammeId = e.ProgrammeId,
                CategorieEmploiId = e.CategorieEmploiId, Effectif = e.Effectif, CreePar = auteurId
            });

        // Cibles : seules les années programmées du nouveau cycle (N à N+2).
        foreach (var c in await ctx.IndicateurCibles.AsNoTracking()
                     .Where(c => c.CycleProgrammationId == ancien.Id && c.Annee >= nouveau.AnneeBase && c.Annee <= fin).ToListAsync())
            ctx.IndicateurCibles.Add(new IndicateurCible
            {
                IndicateurId = c.IndicateurId, CycleProgrammation = nouveau, Annee = c.Annee, ValeurCible = c.ValeurCible, CreePar = auteurId
            });

        // Cadre logique : copie de l'arbre, parents d'abord (objectif général, puis spécifiques, puis résultats).
        var elements = await ctx.CadreLogiqueElements.AsNoTracking().Include(e => e.Risques)
            .Where(e => e.CycleProgrammationId == ancien.Id)
            .OrderBy(e => e.TypeElement).ThenBy(e => e.Ordre)
            .ToListAsync();
        var copies = new Dictionary<Ulid, CadreLogiqueElement>();
        foreach (var e in elements)
        {
            var copie = new CadreLogiqueElement
            {
                ProgrammeId = e.ProgrammeId, CycleProgrammation = nouveau, TypeElement = e.TypeElement, ActionId = e.ActionId,
                ElementParent = e.ElementParentId is { } p && copies.TryGetValue(p, out var parent) ? parent : null,
                Libelle = e.Libelle, IndicateurId = e.IndicateurId, SourceVerificationId = e.SourceVerificationId,
                ValeurCible = e.ValeurCible, Ordre = e.Ordre, CreePar = auteurId
            };
            foreach (var r in e.Risques) copie.Risques.Add(new CadreLogiqueRisque { RisqueId = r.RisqueId, CreePar = auteurId });
            ctx.CadreLogiqueElements.Add(copie);
            copies[e.Id] = copie;
        }
    }
}
