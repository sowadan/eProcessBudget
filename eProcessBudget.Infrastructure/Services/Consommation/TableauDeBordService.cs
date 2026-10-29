using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.BudgetProgramme;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Infrastructure.Services.Recettes;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace eProcessBudget.Infrastructure.Services.Consommation;

/// <summary>Un volet du budget global (Travaux, Investissement, Fonctionnement, Appuis).</summary>
public sealed record VoletBudget(Ulid AxeId, string Volet, MontantsDotation Montants);

/// <summary>Dotation qui atteint un seuil d'alerte.</summary>
public sealed record AlerteSeuil(LigneDotation Dotation, decimal Seuil);

/// <summary>AE et CP programmés d'une année du cycle triennal.</summary>
public sealed record AnneeProgrammee(int Annee, decimal AE, decimal CP);

/// <summary>Recettes d'une catégorie : prévues et réalisées.</summary>
public sealed record RecetteTableauDeBord(string Categorie, MontantsRecette Montants);

/// <summary>Tous les widgets du tableau de bord (cahier fonctionnel § 3.11.2) pour un filtre.</summary>
public sealed record TableauDeBord(
    int Annee, DateOnly? JusquAu, DateTime CalculeLe,
    MontantsDotation Total, decimal? PaiementsProjetes, List<VoletBudget> Volets,
    List<PointEvolution> Evolution,
    List<LigneRegroupement> ParProgramme, List<LigneRegroupement> ParAction, List<LigneRegroupement> ParActivite,
    List<LigneRegroupement> ParRubrique, List<LigneRegroupement> ParSousRubrique, List<LigneRegroupement> ParProjet, List<LigneRegroupement> ParLigne,
    List<LigneRegroupement> ParSite, List<AlerteSeuil> Alertes, int DepassementsEnAttente,
    List<LigneAECP> AECP, List<AnneeProgrammee> Triennal, string? Cycle,
    List<SynthesePerformance> Performance, List<RecetteTableauDeBord> Recettes,
    IndicateursEntretien? Entretien = null)
{
    /// <summary>Les 5 rubriques les plus consommées (montant payé).</summary>
    public IEnumerable<LigneRegroupement> TopRubriques => ParRubrique.Where(r => r.Montants.PaiementsTotal > 0)
        .OrderByDescending(r => r.Montants.PaiementsTotal).Take(5);

    public MontantsRecette TotalRecettes => MontantsRecette.Somme(Recettes.Select(r => r.Montants));
}

/// <summary>
/// Tableau de bord (cahier fonctionnel § 3.11, technique § 4.7) : indicateurs agrégés du
/// budget et du budget-programme, mis en cache 5 minutes par organisme, périmètre de lecture
/// et filtre. Restreint au périmètre de lecture de l'utilisateur.
/// </summary>
public interface ITableauDeBordService
{
    Task<TableauDeBord?> ObtenirAsync(FiltreConsommation filtre, bool forcer = false);
}

public sealed class TableauDeBordService : ITableauDeBordService
{
    public static readonly TimeSpan DureeCache = TimeSpan.FromMinutes(5);
    private const int AlertesMax = 10;
    private const int LignesMax = 10;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesConsommation _acces;
    private readonly IUserContextService _utilisateur;
    private readonly IMemoryCache _cache;

    public TableauDeBordService(IDbContextFactory<AppDbContext> factory, AccesConsommation acces, IUserContextService utilisateur, IMemoryCache cache)
    {
        _factory = factory;
        _acces = acces;
        _utilisateur = utilisateur;
        _cache = cache;
    }

    public async Task<TableauDeBord?> ObtenirAsync(FiltreConsommation filtre, bool forcer = false)
    {
        if (await _acces.LecteurAsync(TypeDroit.TableauDeBordLire) is not { } lecteur) return null;
        var cle = $"tdb:{await _utilisateur.ObtenirOrganismeIdAsync()}:{lecteur.Perimetre.Cle}:{filtre.Cle}";
        if (!forcer && _cache.TryGetValue(cle, out TableauDeBord? enCache) && enCache is not null) return enCache;

        await using var ctx = await _factory.CreateDbContextAsync();
        var tableau = await CalculerAsync(ctx, filtre.Copie(), lecteur.Perimetre);
        if (tableau is not null) _cache.Set(cle, tableau, DureeCache);
        return tableau;
    }

    internal static async Task<TableauDeBord?> CalculerAsync(AppDbContext ctx, FiltreConsommation f, PerimetreLecture perimetre)
    {
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == f.ExerciceId)
            .Select(e => new { e.Annee, e.CycleProgrammationId, Cycle = e.CycleProgrammation != null ? e.CycleProgrammation.Libelle : null })
            .FirstOrDefaultAsync();
        if (exercice is null) return null;

        var consolide = await ConsommationService.ConsoliderAsync(ctx, f, perimetre);
        var dotations = await Consolidation.CalculerAsync(ctx, f, perimetre);
        async Task<List<LigneRegroupement>> Par(Regroupement r) => await Consolidation.RegrouperAsync(ctx, f.ExerciceId, dotations, r);

        // Volets du budget global, dans l'ordre des axes ; un axe sans dotation figure à zéro.
        var axes = await ctx.Axes.AsNoTracking().Where(a => a.TypeAxe == TypeAxe.Depense && a.EstActif)
            .Where(a => f.AxeId == null || a.Id == f.AxeId).OrderBy(a => a.Ordre)
            .Select(a => new { a.Id, a.Code, a.Libelle }).ToListAsync();
        var volets = axes.Select(a => new VoletBudget(a.Id, LibellesConsommation.Volets.GetValueOrDefault(a.Code) ?? a.Libelle,
            consolide.Axes.FirstOrDefault(x => x.AxeId == a.Id)?.Montants ?? MontantsDotation.Zero)).ToList();

        // Alertes : dotations au seuil (ligne > axe > global), les plus engagées d'abord.
        var seuils = await AlerteService.LireSeuilsAsync(ctx);
        var alertes = dotations
            .Select(d => (d, Seuil: CalculsExecution.SeuilAtteint(d.Montants.TauxEngagementYCReports,
                AlerteService.SeuilsApplicables(seuils, d.Dotation.LigneBudgetaireId, d.Dotation.AxeId))))
            .Where(x => x.Seuil is not null)
            .OrderByDescending(x => x.d.Montants.TauxEngagementYCReports)
            .Take(AlertesMax)
            .Select(x => new AlerteSeuil(x.d.Dotation.VersLigne() with { Montants = x.d.Montants }, x.Seuil!.Value))
            .ToList();
        var ids = dotations.Select(d => d.Dotation.Id).ToList();
        var depassements = await ctx.ValidationsDepassement.CountAsync(v => v.Statut == StatutValidation.EnAttente && ids.Contains(v.DotationId));

        // Budget-programme triennal : AE / CP programmés par année du cycle de l'exercice.
        var triennal = new List<AnneeProgrammee>();
        if (exercice.CycleProgrammationId is { } cycleId)
        {
            var q = ctx.ProgrammationsFinancieres.AsNoTracking().Where(p => p.CycleProgrammationId == cycleId);
            if (f.NatureDepenseId is { } nature) q = q.Where(p => p.NatureDepenseId == nature);
            if (f.SourceFinancementId is { } source) q = q.Where(p => p.SourceFinancementId == source);
            if (f.ProgrammeId is { } programme) q = q.Where(p => p.Action.ProgrammeId == programme);
            triennal = await q.GroupBy(p => p.Annee).OrderBy(g => g.Key)
                .Select(g => new AnneeProgrammee(g.Key, g.Sum(p => p.MontantAE), g.Sum(p => p.MontantCP))).ToListAsync();
        }

        // Performance : programmes du périmètre.
        List<Ulid>? programmes = null;
        if (!perimetre.Complet)
        {
            var actions = perimetre.Actions.ToList();
            programmes = perimetre.Programmes
                .Concat(await ctx.Actions.AsNoTracking().Where(a => actions.Contains(a.Id)).Select(a => a.ProgrammeId).ToListAsync())
                .Distinct().ToList();
        }
        if (f.ProgrammeId is { } filtreProgramme) programmes = (programmes ?? [filtreProgramme]).Where(p => p == filtreProgramme).ToList();
        var performance = await IndicateurService.SyntheseAsync(ctx, f.ExerciceId, programmes);

        // Recettes réalisées / prévues, par catégorie (si visibles sur le périmètre).
        var recettes = new List<RecetteTableauDeBord>();
        if (consolide.RecettesVisibles)
        {
            var previsions = await PrevisionRecetteService.Projeter(ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.ExerciceId == f.ExerciceId)).ToListAsync();
            recettes = previsions.GroupBy(p => p.Categorie).OrderBy(g => g.Key)
                .Select(g => new RecetteTableauDeBord(LibellesParametrage.De(LibellesParametrage.CategorieRecette, g.Key), MontantsRecette.Somme(g.Select(p => p.Montants))))
                .ToList();
        }

        // Indicateurs de l'entretien routier (C.2.1), sur le périmètre et les filtres.
        IndicateursEntretien? entretien = await BesoinEntretienService.CalculerAsync(ctx, Consolidation.Source(ctx, f, perimetre), f.ExerciceId, consolide.JusquAu);
        if (entretien.PrevisionsAffectees == 0 && entretien.BesoinsEvalues == 0) entretien = null;

        var parLigne = (await Par(Regroupement.Ligne)).OrderByDescending(l => l.Montants.AETotal).Take(LignesMax).ToList();
        return new TableauDeBord(exercice.Annee, consolide.JusquAu, DateTime.UtcNow,
            consolide.Total, consolide.PaiementsProjetes, volets,
            await ConsommationService.EvolutionAsync(ctx, f, perimetre, TypePeriode.Mois),
            await Par(Regroupement.Programme), await Par(Regroupement.Action), await Par(Regroupement.Activite),
            await Par(Regroupement.Rubrique), await Par(Regroupement.SousRubrique), await Par(Regroupement.Projet), parLigne,
            await Par(Regroupement.Site), alertes, depassements,
            await ConsommationService.AECPAsync(ctx, f, perimetre), triennal, exercice.Cycle,
            performance, recettes, entretien);
    }
}
