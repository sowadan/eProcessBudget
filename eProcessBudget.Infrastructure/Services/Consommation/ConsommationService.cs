using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Infrastructure.Services.Recettes;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Consommation;

/// <summary>Situation consolidée d'un axe : montants du cycle commun, factures reçues, projection des paiements.</summary>
public sealed record LigneConsolidee(Ulid AxeId, string Axe, MontantsDotation Montants, decimal Factures, decimal? PaiementsProjetes)
{
    /// <summary>Taux de consommation projeté en fin d'exercice.</summary>
    public decimal? TauxProjete => PaiementsProjetes is { } p ? CalculsExecution.Taux(p, Montants.DotationTotale) : null;

    public bool Derape => CalculsConsommation.Derape(Montants, PaiementsProjetes);
}

/// <summary>Tableau consolidé tous axes (§ 3.10.2) et recettes en regard.</summary>
public sealed record ConsolideTousAxes(
    List<LigneConsolidee> Axes, decimal PartEcoulee, DateOnly? JusquAu, MontantsRecette Recettes, bool RecettesVisibles)
{
    public MontantsDotation Total => MontantsDotation.Somme(Axes.Select(a => a.Montants));
    public decimal Factures => Axes.Sum(a => a.Factures);
    public decimal? PaiementsProjetes => CalculsConsommation.Projection(Total.PaiementsTotal, PartEcoulee);
    public decimal? TauxProjete => PaiementsProjetes is { } p ? CalculsExecution.Taux(p, Total.DotationTotale) : null;
}

/// <summary>AE et CP d'un programme : programmés pour l'année (cycle de l'exercice), exécutés (engagé, payé ventilés par les clés).</summary>
public sealed record LigneAECP(Ulid? ProgrammeId, string Programme, decimal AEProgramme, decimal CPProgramme, decimal AEExecute, decimal CPExecute)
{
    public decimal? TauxAE => CalculsExecution.Taux(AEExecute, AEProgramme);
    public decimal? TauxCP => CalculsExecution.Taux(CPExecute, CPProgramme);
}

/// <summary>Une dotation qui dérape (§ 3.10.2 : analyse des écarts et identification des dérapages).</summary>
public sealed record LigneDerapage(LigneDotation Dotation, decimal? PaiementsProjetes, string Motif)
{
    public decimal? TauxProjete => PaiementsProjetes is { } p ? CalculsExecution.Taux(p, Dotation.Montants.DotationTotale) : null;
}

/// <summary>Une période de l'évolution chronologique : engagé et payé sur la période, cumuls, prévision linéaire cumulée.</summary>
public sealed record PointEvolution(
    TypePeriode Type, int Numero, DateOnly Debut, DateOnly Fin, decimal Engage, decimal Paye, decimal EngageCumule, decimal PayeCumule, decimal PrevisionCumulee)
{
    public string Libelle => LibellesConsommation.Periode(Type, Numero, Debut);
}

/// <summary>Une dotation, sa rubrique et sa situation (états de situation budgétaire).</summary>
public sealed record LigneSituation(string Axe, string Rubrique, string LigneCode, string LigneLibelle, string Imputation, MontantsDotation Montants);

/// <summary>Exécution d'une nature de dépense sur trois exercices (prévision, réalisé).</summary>
public sealed record LigneTroisExercices(string Nature, IReadOnlyDictionary<int, (decimal Prevision, decimal Realise)> Annees);

/// <summary>Niveau du tableau à double entrée (§ 3.11.3).</summary>
public enum NiveauCroisement
{
    ProgrammeRubrique = 1,
    ActionSousRubrique = 2,
    ActiviteLigne = 3,
}

public sealed record EnTeteCroisement(Ulid? Id, string Libelle);
public sealed record CelluleCroisee(Ulid? Ligne, Ulid? Colonne, MontantsDotation Montants);
public sealed record TableauCroise(List<EnTeteCroisement> Lignes, List<EnTeteCroisement> Colonnes, List<CelluleCroisee> Cellules)
{
    public MontantsDotation Cellule(Ulid? ligne, Ulid? colonne)
        => Cellules.FirstOrDefault(c => c.Ligne == ligne && c.Colonne == colonne)?.Montants ?? MontantsDotation.Zero;
}

/// <summary>
/// Consommation budgétaire (cahier fonctionnel § 3.10, technique § 4.7) : vision consolidée
/// de tous les axes de dépenses — tableau consolidé, consolidations par programme, action,
/// activité, nature, rubrique..., AE et CP programmés / exécutés, projection de fin d'exercice,
/// dérapages, évolution chronologique, tableau à double entrée. Restreinte au périmètre de
/// lecture de l'utilisateur.
/// </summary>
public interface IConsommationService
{
    Task<ConsolideTousAxes?> ConsoliderAsync(FiltreConsommation filtre);
    Task<List<LigneRegroupement>> RegrouperAsync(FiltreConsommation filtre, Regroupement par);
    Task<List<LigneAECP>> AECPParProgrammeAsync(FiltreConsommation filtre);
    Task<List<LigneDerapage>> DerapagesAsync(FiltreConsommation filtre);
    Task<List<PointEvolution>> EvolutionAsync(FiltreConsommation filtre, TypePeriode granularite);
    Task<TableauCroise?> CroiserAsync(FiltreConsommation filtre, NiveauCroisement niveau);

    /// <summary>Chaque dotation retenue, avec sa rubrique, triée par axe, rubrique et ligne.</summary>
    Task<List<LigneSituation>> SituationDetailleeAsync(FiltreConsommation filtre);

    /// <summary>
    /// Exécution par nature de dépense sur l'exercice et les deux précédents : situation
    /// courante pour un exercice non clôturé, historique d'exécution sinon (§ 3.12.2).
    /// </summary>
    Task<(List<LigneTroisExercices> Lignes, int[] Annees)> ExecutionTroisExercicesAsync(Ulid exerciceId);
}

public sealed class ConsommationService : IConsommationService
{
    private const int DerapagesMax = 100;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesConsommation _acces;

    public ConsommationService(IDbContextFactory<AppDbContext> factory, AccesConsommation acces)
    {
        _factory = factory;
        _acces = acces;
    }

    private Task<(Ulid UtilisateurId, PerimetreLecture Perimetre)?> LecteurAsync() => _acces.LecteurAsync(TypeDroit.ConsommationLire);

    public async Task<ConsolideTousAxes?> ConsoliderAsync(FiltreConsommation f)
    {
        if (await LecteurAsync() is not { } lecteur) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ConsoliderAsync(ctx, f, lecteur.Perimetre);
    }

    public async Task<List<LigneRegroupement>> RegrouperAsync(FiltreConsommation f, Regroupement par)
    {
        if (await LecteurAsync() is not { } lecteur) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var dotations = await Consolidation.CalculerAsync(ctx, f, lecteur.Perimetre);
        return await Consolidation.RegrouperAsync(ctx, f.ExerciceId, dotations, par);
    }

    public async Task<List<LigneAECP>> AECPParProgrammeAsync(FiltreConsommation f)
    {
        if (await LecteurAsync() is not { } lecteur) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await AECPAsync(ctx, f, lecteur.Perimetre);
    }

    public async Task<List<LigneDerapage>> DerapagesAsync(FiltreConsommation f)
    {
        if (await LecteurAsync() is not { } lecteur) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var part = await PartEcouleeAsync(ctx, f);
        var dotations = await Consolidation.CalculerAsync(ctx, f, lecteur.Perimetre);
        return dotations
            .Select(d => (d, Projection: CalculsConsommation.Projection(d.Montants.PaiementsTotal, part)))
            .Where(x => CalculsConsommation.Derape(x.d.Montants, x.Projection))
            .OrderBy(x => x.d.Montants.Disponible)
            .ThenByDescending(x => x.Projection - x.d.Montants.DotationTotale)
            .Take(DerapagesMax)
            .Select(x => new LigneDerapage(x.d.Dotation.VersLigne() with { Montants = x.d.Montants }, x.Projection,
                x.d.Montants.Disponible < 0 ? "Engagements au-delà de la dotation totale" : "Paiements projetés au-delà de la dotation totale"))
            .ToList();
    }

    public async Task<List<PointEvolution>> EvolutionAsync(FiltreConsommation f, TypePeriode granularite)
    {
        if (await LecteurAsync() is not { } lecteur) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await EvolutionAsync(ctx, f, lecteur.Perimetre, granularite);
    }

    public async Task<TableauCroise?> CroiserAsync(FiltreConsommation f, NiveauCroisement niveau)
    {
        if (await LecteurAsync() is not { } lecteur) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var dotations = await Consolidation.CalculerAsync(ctx, f, lecteur.Perimetre);
        var (lignes, colonnes) = niveau switch
        {
            NiveauCroisement.ActionSousRubrique => (Regroupement.Action, Regroupement.SousRubrique),
            NiveauCroisement.ActiviteLigne => (Regroupement.Activite, Regroupement.Ligne),
            _ => (Regroupement.Programme, Regroupement.Rubrique),
        };
        var partsLignes = await Consolidation.PartsAsync(ctx, f.ExerciceId, dotations, lignes);
        var colonneDe = (await Consolidation.PartsAsync(ctx, f.ExerciceId, dotations, colonnes))
            .GroupBy(p => p.DotationId).ToDictionary(g => g.Key, g => (g.First().Cle, g.First().Libelle));

        var cellules = partsLignes
            .GroupBy(p => (p.Cle, Colonne: colonneDe[p.DotationId].Cle))
            .Select(g => new CelluleCroisee(g.Key.Cle, g.Key.Colonne, MontantsDotation.Somme(g.Select(p => p.Montants))))
            .ToList();
        var entetesLignes = partsLignes.GroupBy(p => p.Cle).Select(g => new EnTeteCroisement(g.Key, g.First().Libelle))
            .OrderBy(e => e.Id is null).ThenBy(e => e.Libelle, StringComparer.CurrentCulture).ToList();
        var entetesColonnes = colonneDe.Values.DistinctBy(c => c.Cle).Select(c => new EnTeteCroisement(c.Cle, c.Libelle))
            .OrderBy(e => e.Id is null).ThenBy(e => e.Libelle, StringComparer.CurrentCulture).ToList();
        return new TableauCroise(entetesLignes, entetesColonnes, cellules);
    }

    public async Task<List<LigneSituation>> SituationDetailleeAsync(FiltreConsommation f)
    {
        if (await LecteurAsync() is not { } lecteur) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var dotations = await Consolidation.CalculerAsync(ctx, f, lecteur.Perimetre);
        var rubriques = await ctx.Rubriques.AsNoTracking().ToDictionaryAsync(r => r.Id, r => (r.Code + " — " + r.Libelle, r.Ordre));
        var axes = await ctx.Axes.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Ordre);
        return dotations
            .OrderBy(d => axes.GetValueOrDefault(d.Dotation.AxeId))
            .ThenBy(d => rubriques.GetValueOrDefault(d.Dotation.RubriqueId).Ordre).ThenBy(d => d.Dotation.Rubrique)
            .ThenBy(d => d.Dotation.LigneCode).ThenBy(d => d.Dotation.Id)
            .Select(d => new LigneSituation(d.Dotation.Axe, rubriques.GetValueOrDefault(d.Dotation.RubriqueId).Item1 ?? d.Dotation.Rubrique,
                d.Dotation.LigneCode, d.Dotation.LigneLibelle, d.Dotation.VersLigne().Imputation, d.Montants))
            .ToList();
    }

    public async Task<(List<LigneTroisExercices> Lignes, int[] Annees)> ExecutionTroisExercicesAsync(Ulid exerciceId)
    {
        if (await LecteurAsync() is not { } lecteur) return ([], []);
        await using var ctx = await _factory.CreateDbContextAsync();
        var annee = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => (int?)e.Annee).FirstOrDefaultAsync();
        if (annee is not { } n) return ([], []);
        int[] annees = [n - 2, n - 1, n];
        var natures = await ctx.NaturesDepense.AsNoTracking().ToDictionaryAsync(x => x.Id, x => (x.Code + " — " + x.Libelle, x.Ordre));

        // Clé : la nature de dépense, Ulid.Empty pour une ligne sans nature.
        var parAnnee = new Dictionary<int, Dictionary<Ulid, (decimal Prevision, decimal Realise)>>();
        foreach (var a in annees)
        {
            var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Annee == a).Select(e => new { e.Id, e.Statut }).FirstOrDefaultAsync();
            if (exercice is not null && exercice.Statut != StatutExercice.Cloture)
            {
                var dotations = await Consolidation.CalculerAsync(ctx, new FiltreConsommation { ExerciceId = exercice.Id }, lecteur.Perimetre);
                parAnnee[a] = dotations.GroupBy(d => d.Dotation.NatureDepenseId ?? Ulid.Empty)
                    .ToDictionary(g => g.Key, g => (g.Sum(d => d.Montants.PrevisionAjustee), g.Sum(d => d.Montants.PaiementsTotal)));
            }
            else
            {
                // Exercice clôturé ou repris : l'historique d'exécution (lignes de dépense), sur le périmètre des axes.
                var q = ctx.ExecutionsHistoriques.AsNoTracking().Where(h => h.Annee == a && h.NatureRecetteId == null);
                if (!lecteur.Perimetre.Complet)
                {
                    var axes = lecteur.Perimetre.Axes.ToList();
                    q = q.Where(h => h.AxeId != null && axes.Contains(h.AxeId.Value));
                }
                parAnnee[a] = (await q.GroupBy(h => h.NatureDepenseId ?? Ulid.Empty)
                        .Select(g => new { g.Key, Prevision = g.Sum(h => h.Prevision), Realise = g.Sum(h => h.MontantRealise ?? 0) })
                        .ToListAsync())
                    .ToDictionary(x => x.Key, x => (x.Prevision, x.Realise));
            }
        }

        var lignes = parAnnee.Values.SelectMany(d => d.Keys).Distinct()
            .OrderBy(k => k == Ulid.Empty).ThenBy(k => natures.TryGetValue(k, out var nat) ? nat.Ordre : int.MaxValue)
            .Select(k => new LigneTroisExercices(natures.TryGetValue(k, out var nat) ? nat.Item1 : "Nature non précisée",
                annees.ToDictionary(a => a, a => parAnnee[a].GetValueOrDefault(k))))
            .ToList();
        return (lignes, annees);
    }

    // ═════ Calculs partagés avec le tableau de bord ═══════════════════════════

    /// <summary>Part écoulée de l'exercice à la date de la situation (fin de période, aujourd'hui, ou fin d'un exercice clôturé).</summary>
    internal static async Task<decimal> PartEcouleeAsync(AppDbContext ctx, FiltreConsommation f)
    {
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == f.ExerciceId)
            .Select(e => new { e.DateDebut, e.DateFin, e.Statut }).FirstOrDefaultAsync();
        if (exercice is null) return 0;
        if (exercice.Statut == StatutExercice.Cloture) return 1;
        var date = await Consolidation.JusquAuAsync(ctx, f) ?? DateOnly.FromDateTime(DateTime.Today);
        return CalculsConsommation.PartEcoulee(exercice.DateDebut, exercice.DateFin, date);
    }

    internal static async Task<ConsolideTousAxes> ConsoliderAsync(AppDbContext ctx, FiltreConsommation f, PerimetreLecture perimetre)
    {
        var jusquAu = await Consolidation.JusquAuAsync(ctx, f);
        var part = await PartEcouleeAsync(ctx, f);
        var dotations = await Consolidation.CalculerAsync(ctx, f, perimetre);

        // Factures reçues sur les engagements validés des dotations retenues (pondérées comme leurs dotations).
        var ids = dotations.Select(d => d.Dotation.Id).ToList();
        var factures = await ctx.Factures.AsNoTracking()
            .Where(x => ids.Contains(x.Engagement.DotationId) && x.Engagement.Statut == StatutSaisie.Valide && x.Statut == StatutSaisie.Valide
                     && (jusquAu == null || x.DateReception <= jusquAu))
            .GroupBy(x => new { x.Engagement.DotationId, x.Engagement.Dotation.LigneBudgetaireId })
            .Select(g => new { g.Key.DotationId, g.Key.LigneBudgetaireId, Montant = g.Sum(x => x.Montant) })
            .ToListAsync();
        var parts = await Consolidation.PartsProgrammeAsync(ctx, f, factures.Select(x => x.LigneBudgetaireId));
        var facturesParDotation = factures.ToDictionary(x => x.DotationId,
            x => parts is null ? x.Montant : CalculsAxes.Part(x.Montant, parts.GetValueOrDefault(x.LigneBudgetaireId)));

        var ordre = await ctx.Axes.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Ordre);
        var axes = dotations.GroupBy(d => (d.Dotation.AxeId, d.Dotation.Axe))
            .OrderBy(g => ordre.GetValueOrDefault(g.Key.AxeId))
            .Select(g =>
            {
                var m = MontantsDotation.Somme(g.Select(d => d.Montants));
                return new LigneConsolidee(g.Key.AxeId, g.Key.Axe, m, g.Sum(d => facturesParDotation.GetValueOrDefault(d.Dotation.Id)),
                    CalculsConsommation.Projection(m.PaiementsTotal, part));
            })
            .ToList();

        // Recettes en regard (factures émises et recouvrées), visibles sur tout le périmètre ou sur l'axe Recettes.
        var axeRecettes = await ctx.Axes.AsNoTracking().Where(a => a.TypeAxe == TypeAxe.Recette).Select(a => (Ulid?)a.Id).FirstOrDefaultAsync();
        var recettesVisibles = perimetre.Complet || (axeRecettes is { } ar && perimetre.Axes.Contains(ar));
        var recettes = MontantsRecette.Zero;
        if (recettesVisibles && f.AxeId is null)
        {
            var previsions = await PrevisionRecetteService.Projeter(ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.ExerciceId == f.ExerciceId)).ToListAsync();
            recettes = MontantsRecette.Somme(previsions.Select(p => p.Montants));
        }
        return new ConsolideTousAxes(axes, part, jusquAu, recettes, recettesVisibles && f.AxeId is null);
    }

    internal static async Task<List<LigneAECP>> AECPAsync(AppDbContext ctx, FiltreConsommation f, PerimetreLecture perimetre)
    {
        var dotations = await Consolidation.CalculerAsync(ctx, f, perimetre);
        var executes = (await Consolidation.PartsAsync(ctx, f.ExerciceId, dotations, Regroupement.Programme))
            .GroupBy(p => p.Cle)
            .ToDictionary(g => g.Key ?? Ulid.Empty, g => (g.First().Libelle, AE: g.Sum(p => p.Montants.AETotal), CP: g.Sum(p => p.Montants.PaiementsTotal)));

        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == f.ExerciceId).Select(e => new { e.Annee, e.CycleProgrammationId }).FirstAsync();
        var programmes = new Dictionary<Ulid, (string Libelle, decimal AE, decimal CP)>();
        if (exercice.CycleProgrammationId is { } cycleId)
        {
            var q = ctx.ProgrammationsFinancieres.AsNoTracking().Where(p => p.CycleProgrammationId == cycleId && p.Annee == exercice.Annee);
            if (f.NatureDepenseId is { } nature) q = q.Where(p => p.NatureDepenseId == nature);
            if (f.SourceFinancementId is { } source) q = q.Where(p => p.SourceFinancementId == source);
            if (f.ProgrammeId is { } programme) q = q.Where(p => p.Action.ProgrammeId == programme);
            if (!perimetre.Complet)
            {
                var progs = perimetre.Programmes.ToList();
                var actions = perimetre.Actions.ToList();
                q = q.Where(p => progs.Contains(p.Action.ProgrammeId) || actions.Contains(p.ActionId));
            }
            programmes = (await q.GroupBy(p => new { p.Action.ProgrammeId, Libelle = p.Action.Programme.Code + " — " + p.Action.Programme.Libelle })
                    .Select(g => new { g.Key.ProgrammeId, g.Key.Libelle, AE = g.Sum(p => p.MontantAE), CP = g.Sum(p => p.MontantCP) })
                    .ToListAsync())
                .ToDictionary(x => x.ProgrammeId, x => (x.Libelle, x.AE, x.CP));
        }

        return programmes.Keys.Union(executes.Keys)
            .Select(id =>
            {
                var prog = programmes.GetValueOrDefault(id);
                var exec = executes.GetValueOrDefault(id);
                return new LigneAECP(id == Ulid.Empty ? null : id, prog.Libelle ?? exec.Libelle ?? "Non réparti", prog.AE, prog.CP, exec.AE, exec.CP);
            })
            .OrderBy(l => l.ProgrammeId is null).ThenBy(l => l.Programme, StringComparer.CurrentCulture)
            .ToList();
    }

    internal static async Task<List<PointEvolution>> EvolutionAsync(AppDbContext ctx, FiltreConsommation f, PerimetreLecture perimetre, TypePeriode granularite)
    {
        var periodes = await ctx.Periodes.AsNoTracking().Where(p => p.ExerciceId == f.ExerciceId && p.TypePeriode == granularite)
            .OrderBy(p => p.Numero).Select(p => new { p.Numero, p.DateDebut, p.DateFin }).ToListAsync();
        if (periodes.Count == 0) return [];
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == f.ExerciceId).Select(e => new { e.DateDebut, e.DateFin }).FirstAsync();
        var jusquAu = await Consolidation.JusquAuAsync(ctx, f);

        var source = Consolidation.Source(ctx, f, perimetre).Select(d => d.Id);
        var engagements = await ctx.Engagements.AsNoTracking()
            .Where(e => source.Contains(e.DotationId) && e.Statut == StatutSaisie.Valide && !e.EstReport)
            .Select(e => new { e.Dotation.LigneBudgetaireId, Date = e.DateEngagement, e.Montant }).ToListAsync();
        var paiements = await ctx.Paiements.AsNoTracking()
            .Where(p => source.Contains(p.Engagement.DotationId) && p.Statut == StatutSaisie.Valide && p.Engagement.Statut == StatutSaisie.Valide)
            .Select(p => new { p.Engagement.Dotation.LigneBudgetaireId, Date = p.DatePaiement, p.Montant }).ToListAsync();
        var dotationTotale = MontantsDotation.Somme((await Consolidation.CalculerAsync(ctx, f, perimetre)).Select(d => d.Montants)).DotationTotale;
        var parts = await Consolidation.PartsProgrammeAsync(ctx, f, engagements.Select(e => e.LigneBudgetaireId).Concat(paiements.Select(p => p.LigneBudgetaireId)));
        decimal Pondere(Ulid ligne, decimal montant) => parts is null ? montant : CalculsAxes.Part(montant, parts.GetValueOrDefault(ligne));

        decimal engageCumule = 0, payeCumule = 0;
        var points = new List<PointEvolution>();
        foreach (var p in periodes.Where(p => jusquAu is null || p.DateDebut <= jusquAu))
        {
            var engage = engagements.Where(e => e.Date >= p.DateDebut && e.Date <= p.DateFin).Sum(e => Pondere(e.LigneBudgetaireId, e.Montant));
            var paye = paiements.Where(x => x.Date >= p.DateDebut && x.Date <= p.DateFin).Sum(x => Pondere(x.LigneBudgetaireId, x.Montant));
            engageCumule += engage;
            payeCumule += paye;
            var prevision = Math.Round(dotationTotale * CalculsConsommation.PartEcoulee(exercice.DateDebut, exercice.DateFin, p.DateFin), 2);
            points.Add(new PointEvolution(granularite, p.Numero, p.DateDebut, p.DateFin, engage, paye, engageCumule, payeCumule, prevision));
        }
        return points;
    }
}
