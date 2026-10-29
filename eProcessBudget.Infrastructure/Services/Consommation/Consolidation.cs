using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Consommation;

/// <summary>Filtres des vues consolidées (cahier fonctionnel § 3.11.3).</summary>
public sealed class FiltreConsommation
{
    public Ulid ExerciceId { get; set; }
    public Ulid? AxeId { get; set; }
    public Ulid? NatureDepenseId { get; set; }

    /// <summary>Source de financement (RI / RE).</summary>
    public Ulid? SourceFinancementId { get; set; }

    public Ulid? SiteId { get; set; }

    /// <summary>Service.</summary>
    public Ulid? StructureId { get; set; }

    /// <summary>Programme : seule la part des lignes qui lui revient (clés de répartition) est retenue.</summary>
    public Ulid? ProgrammeId { get; set; }

    /// <summary>Période (mois, trimestre, semestre) : situation à sa date de fin. Sans période, la situation courante.</summary>
    public Ulid? PeriodeId { get; set; }

    /// <summary>Clé du filtre, pour un cache.</summary>
    public string Cle => $"{ExerciceId}|{AxeId}|{NatureDepenseId}|{SourceFinancementId}|{SiteId}|{StructureId}|{ProgrammeId}|{PeriodeId}";

    public FiltreConsommation Copie() => (FiltreConsommation)MemberwiseClone();
}

/// <summary>Une dotation et ses montants retenus (pondérés par la part du programme filtré, le cas échéant).</summary>
internal sealed record DotationRetenue(DotationCalculee Dotation, MontantsDotation Montants);

/// <summary>
/// Moteur des vues consolidées : sélection des dotations (exercice, filtres, périmètre de
/// lecture), situation à la date de fin de période, regroupements. Tout part de
/// <see cref="SituationService.Calculer(AppDbContext, IQueryable{Dotation}, DateOnly?)"/> :
/// mêmes conventions que le cycle commun.
/// </summary>
internal static class Consolidation
{
    private const string NonAffecte = "Non affecté";

    /// <summary>Dotations de l'exercice retenues par les filtres et le périmètre de lecture.</summary>
    public static IQueryable<Dotation> Source(AppDbContext ctx, FiltreConsommation f, PerimetreLecture perimetre)
    {
        var q = ctx.Dotations.AsNoTracking().Where(d => d.ExerciceId == f.ExerciceId);
        if (f.AxeId is { } axe) q = q.Where(d => d.LigneBudgetaire.SousRubrique.Rubrique.AxeId == axe);
        if (f.NatureDepenseId is { } nature) q = q.Where(d => d.LigneBudgetaire.NatureDepenseId == nature);
        if (f.SourceFinancementId is { } source) q = q.Where(d => d.LigneBudgetaire.SourceFinancementId == source);
        if (f.SiteId is { } site) q = q.Where(d => d.SiteId == site);
        if (f.StructureId is { } structure) q = q.Where(d => d.StructureId == structure);
        if (f.ProgrammeId is { } programme)
            q = q.Where(d => ctx.ClesRepartition.Any(c => c.ExerciceId == d.ExerciceId && c.LigneBudgetaireId == d.LigneBudgetaireId
                                                        && c.Activite.Action.ProgrammeId == programme));

        if (!perimetre.Complet)
        {
            var axes = perimetre.Axes.ToList();
            var sites = perimetre.Sites.ToList();
            var structures = perimetre.Structures.ToList();
            var programmes = perimetre.Programmes.ToList();
            var actions = perimetre.Actions.ToList();
            q = q.Where(d => axes.Contains(d.LigneBudgetaire.SousRubrique.Rubrique.AxeId)
                          || (d.SiteId != null && sites.Contains(d.SiteId.Value))
                          || (d.StructureId != null && structures.Contains(d.StructureId.Value))
                          || ctx.ClesRepartition.Any(c => c.ExerciceId == d.ExerciceId && c.LigneBudgetaireId == d.LigneBudgetaireId
                                                       && (programmes.Contains(c.Activite.Action.ProgrammeId) || actions.Contains(c.Activite.ActionId))));
        }
        return q;
    }

    /// <summary>Date de fin de la période filtrée, ou null (situation courante).</summary>
    public static async Task<DateOnly?> JusquAuAsync(AppDbContext ctx, FiltreConsommation f)
        => f.PeriodeId is { } periode
            ? await ctx.Periodes.AsNoTracking().Where(p => p.Id == periode && p.ExerciceId == f.ExerciceId).Select(p => (DateOnly?)p.DateFin).FirstOrDefaultAsync()
            : null;

    /// <summary>Dotations retenues et leur situation ; avec un programme filtré, seule sa part des montants.</summary>
    public static async Task<List<DotationRetenue>> CalculerAsync(AppDbContext ctx, FiltreConsommation f, PerimetreLecture perimetre)
    {
        var jusquAu = await JusquAuAsync(ctx, f);
        var dotations = await SituationService.Calculer(ctx, Source(ctx, f, perimetre), jusquAu).ToListAsync();
        var parts = await PartsProgrammeAsync(ctx, f, dotations.Select(d => d.LigneBudgetaireId));
        return dotations.Select(d => new DotationRetenue(d, parts is null ? d.Montants : CalculsAxes.Repartir(d.Montants, parts.GetValueOrDefault(d.LigneBudgetaireId))))
                        .ToList();
    }

    /// <summary>Part (en %) de chaque ligne revenant au programme filtré ; null sans programme filtré.</summary>
    public static async Task<Dictionary<Ulid, decimal>?> PartsProgrammeAsync(AppDbContext ctx, FiltreConsommation f, IEnumerable<Ulid> lignesBudgetaires)
    {
        if (f.ProgrammeId is not { } programme) return null;
        var lignes = lignesBudgetaires.Distinct().ToList();
        return await ctx.ClesRepartition.AsNoTracking()
            .Where(c => c.ExerciceId == f.ExerciceId && lignes.Contains(c.LigneBudgetaireId) && c.Activite.Action.ProgrammeId == programme)
            .GroupBy(c => c.LigneBudgetaireId).Select(g => new { g.Key, Part = g.Sum(c => c.Pourcentage) })
            .ToDictionaryAsync(x => x.Key, x => x.Part);
    }

    /// <summary>Regroupe la situation des dotations selon un critère ; une dotation répartie entre activités contribue à plusieurs regroupements.</summary>
    public static async Task<List<LigneRegroupement>> RegrouperAsync(AppDbContext ctx, Ulid exerciceId, IReadOnlyList<DotationRetenue> dotations, Regroupement par)
    {
        if (dotations.Count == 0) return [];
        var parts = await PartsAsync(ctx, exerciceId, dotations, par);
        return parts
            .GroupBy(p => p.Cle)
            .Select(g => new LigneRegroupement(g.Key, g.First().Libelle, MontantsDotation.Somme(g.Select(p => p.Montants)),
                g.Select(p => p.DotationId).Distinct().Count()))
            .OrderBy(l => l.Id is null)
            .ThenBy(l => l.Libelle, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>Contribution de chaque dotation à chaque regroupement : (clé, libellé, montants, dotation).</summary>
    public static async Task<List<(Ulid? Cle, string Libelle, MontantsDotation Montants, Ulid DotationId)>> PartsAsync(
        AppDbContext ctx, Ulid exerciceId, IReadOnlyList<DotationRetenue> dotations, Regroupement par)
    {
        switch (par)
        {
            case Regroupement.Axe:
            {
                return dotations.Select(d => ((Ulid?)d.Dotation.AxeId, d.Dotation.Axe, d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Projet:
            {
                var ids = dotations.Select(d => d.Dotation.ProjetId).OfType<Ulid>().Distinct().ToList();
                var libelles = await ctx.Projets.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code + " — " + p.Intitule);
                return dotations.Select(d => (d.Dotation.ProjetId, Libelle(libelles, d.Dotation.ProjetId), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Site:
            {
                var ids = dotations.Select(d => d.Dotation.SiteId).OfType<Ulid>().Distinct().ToList();
                var libelles = await ctx.Sites.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code + " — " + s.Nom);
                return dotations.Select(d => (d.Dotation.SiteId, Libelle(libelles, d.Dotation.SiteId), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Structure:
            {
                var ids = dotations.Select(d => d.Dotation.StructureId).OfType<Ulid>().Distinct().ToList();
                var libelles = await ctx.Structures.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code + " — " + s.Libelle);
                return dotations.Select(d => (d.Dotation.StructureId, Libelle(libelles, d.Dotation.StructureId), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Entite:
            {
                var entites = await EntitesAsync(ctx, dotations.Select(d => d.Dotation).ToList());
                var ids = entites.Values.OfType<Ulid>().Distinct().ToList();
                var libelles = await ctx.Tiers.AsNoTracking().Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Code + " — " + t.RaisonSociale);
                return dotations.Select(d => (entites[d.Dotation.Id], Libelle(libelles, entites[d.Dotation.Id]), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Rubrique:
            {
                var ids = dotations.Select(d => d.Dotation.RubriqueId).Distinct().ToList();
                var libelles = await ctx.Rubriques.AsNoTracking().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Code + " — " + r.Libelle);
                return dotations.Select(d => ((Ulid?)d.Dotation.RubriqueId, Libelle(libelles, d.Dotation.RubriqueId), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.SousRubrique:
            {
                var ids = dotations.Select(d => d.Dotation.SousRubriqueId).Distinct().ToList();
                var libelles = await ctx.SousRubriques.AsNoTracking().Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Code + " — " + r.Libelle);
                return dotations.Select(d => ((Ulid?)d.Dotation.SousRubriqueId, Libelle(libelles, d.Dotation.SousRubriqueId), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Ligne:
            {
                return dotations.Select(d => ((Ulid?)d.Dotation.LigneBudgetaireId, d.Dotation.LigneCode + " — " + d.Dotation.LigneLibelle, d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Nature:
            {
                var ids = dotations.Select(d => d.Dotation.NatureDepenseId).OfType<Ulid>().Distinct().ToList();
                var libelles = await ctx.NaturesDepense.AsNoTracking().Where(n => ids.Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.Code + " — " + n.Libelle);
                return dotations.Select(d => (d.Dotation.NatureDepenseId, Libelle(libelles, d.Dotation.NatureDepenseId), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Source:
            {
                var ids = dotations.Select(d => d.Dotation.SourceFinancementId).Distinct().ToList();
                var libelles = await ctx.SourcesFinancement.AsNoTracking().Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Code + " — " + s.Libelle);
                return dotations.Select(d => ((Ulid?)d.Dotation.SourceFinancementId, Libelle(libelles, d.Dotation.SourceFinancementId), d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Compte:
            {
                // Compte principal de la ligne, sinon le premier rattaché.
                var lignes = dotations.Select(d => d.Dotation.LigneBudgetaireId).Distinct().ToList();
                var comptes = (await ctx.LignesComptes.AsNoTracking().Where(lc => lignes.Contains(lc.LigneBudgetaireId))
                        .Select(lc => new { lc.LigneBudgetaireId, lc.EstPrincipal, lc.CompteComptableId, L = lc.CompteComptable.Numero + " — " + lc.CompteComptable.Libelle })
                        .ToListAsync())
                    .GroupBy(lc => lc.LigneBudgetaireId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.EstPrincipal).First());
                return dotations.Select(d => comptes.TryGetValue(d.Dotation.LigneBudgetaireId, out var c)
                    ? ((Ulid?)c.CompteComptableId, c.L, d.Montants, d.Dotation.Id)
                    : ((Ulid?)null, NonAffecte, d.Montants, d.Dotation.Id)).ToList();
            }
            case Regroupement.Activite:
            case Regroupement.Action:
            case Regroupement.Programme:
                return await PartsBudgetProgrammeAsync(ctx, exerciceId, dotations, par);
            default:
                return [];
        }
    }

    /// <summary>
    /// Part de chaque dotation revenant à chaque activité (puis à son action et à son
    /// programme) selon les clés de répartition de sa ligne pour l'exercice. Une ligne sans
    /// clé reste « non répartie ».
    /// </summary>
    private static async Task<List<(Ulid? Cle, string Libelle, MontantsDotation Montants, Ulid DotationId)>> PartsBudgetProgrammeAsync(
        AppDbContext ctx, Ulid exerciceId, IReadOnlyList<DotationRetenue> dotations, Regroupement par)
    {
        var lignes = dotations.Select(d => d.Dotation.LigneBudgetaireId).Distinct().ToList();
        var cles = (await ctx.ClesRepartition.AsNoTracking()
                .Where(c => c.ExerciceId == exerciceId && lignes.Contains(c.LigneBudgetaireId))
                .Select(c => new
                {
                    c.LigneBudgetaireId, c.Pourcentage,
                    ActiviteId = c.ActiviteId, Activite = c.Activite.Code + " — " + c.Activite.Libelle,
                    ActionId = c.Activite.ActionId, Action = c.Activite.Action.Code + " — " + c.Activite.Action.Libelle,
                    ProgrammeId = c.Activite.Action.ProgrammeId, Programme = c.Activite.Action.Programme.Code + " — " + c.Activite.Action.Programme.Libelle
                })
                .ToListAsync())
            .ToLookup(c => c.LigneBudgetaireId);

        var parts = new List<(Ulid?, string, MontantsDotation, Ulid)>();
        foreach (var d in dotations)
        {
            var clesLigne = cles[d.Dotation.LigneBudgetaireId].ToList();
            if (clesLigne.Count == 0)
            {
                parts.Add((null, "Non réparti (ligne sans clé de répartition)", d.Montants, d.Dotation.Id));
                continue;
            }
            foreach (var c in clesLigne)
            {
                var (cle, libelle) = par switch
                {
                    Regroupement.Programme => (c.ProgrammeId, c.Programme),
                    Regroupement.Action => (c.ActionId, c.Action),
                    _ => (c.ActiviteId, c.Activite),
                };
                parts.Add((cle, libelle, CalculsAxes.Repartir(d.Montants, c.Pourcentage), d.Dotation.Id));
            }
        }
        return parts;
    }

    /// <summary>Entité bénéficiaire de chaque dotation : la sienne, sinon celle de son projet.</summary>
    public static async Task<Dictionary<Ulid, Ulid?>> EntitesAsync(AppDbContext ctx, IReadOnlyList<DotationCalculee> dotations)
    {
        var projetIds = dotations.Where(d => d.TiersId is null && d.ProjetId is not null).Select(d => d.ProjetId!.Value).Distinct().ToList();
        var beneficiaires = projetIds.Count == 0 ? [] : await ctx.Projets.AsNoTracking()
            .Where(p => projetIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.TiersBeneficiaireId);
        return dotations.ToDictionary(d => d.Id, d => d.TiersId ?? (d.ProjetId is { } p ? beneficiaires.GetValueOrDefault(p) : null));
    }

    private static string Libelle(Dictionary<Ulid, string> libelles, Ulid? id)
        => id is { } i ? libelles.GetValueOrDefault(i) ?? NonAffecte : NonAffecte;
}
