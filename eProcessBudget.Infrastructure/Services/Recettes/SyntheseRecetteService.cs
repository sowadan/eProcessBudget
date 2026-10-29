using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Recettes;

/// <summary>Critère de regroupement des recettes.</summary>
public enum RegroupementRecette
{
    Categorie = 1,
    Nature = 2,
    Site = 3,
    Collecteur = 4,
    Ligne = 5,
}

/// <summary>Situation cumulée d'un regroupement de prévisions.</summary>
/// <param name="Programme">Ressource programmée pour l'année dans le cycle de l'exercice (par nature seulement).</param>
public sealed record LigneSyntheseRecette(Ulid? Id, string Libelle, MontantsRecette Montants, decimal? Programme = null);

/// <summary>Indicateurs clés des recettes (cahier fonctionnel § 3.9.3).</summary>
/// <param name="MoisCompares">Nombre de mois comparés pour le taux d'accroissement (période comparable).</param>
public sealed record IndicateursRecettes(
    MontantsRecette Montants, decimal Reports, decimal ReportsNonPositionnes, decimal RessourcesAdditionnelles,
    decimal EncaisseN1Comparable, int MoisCompares)
{
    /// <summary>Niveau des recettes : total encaissé sur l'exercice.</summary>
    public decimal Niveau => Montants.Encaisse;

    /// <summary>Taux d'accroissement des ressources par rapport à N-1, sur la même période.</summary>
    public decimal? TauxAccroissement => CalculsRecettes.TauxAccroissement(Montants.Encaisse, EncaisseN1Comparable);

    /// <summary>Ressources acquises mais non positionnées : encaissements et reports hors compte bancaire.</summary>
    public decimal NonPositionne => Montants.EncaisseNonPositionne + ReportsNonPositionnes;
}

/// <summary>Encaissé d'un mois, sur l'exercice et sur le même mois de N-1.</summary>
public sealed record LigneMoisRecette(int Mois, DateOnly Debut, decimal Encaisse, decimal EncaisseN1, decimal PrevuCumule);

/// <summary>Recettes d'un site sur trois exercices (C.2.13) ; null : aucune donnée pour l'année.</summary>
public sealed record ComparaisonSiteRecette(Ulid? SiteId, string Site, decimal? AnneeN2, decimal? AnneeN1, decimal AnneeN, decimal PrevuN)
{
    public decimal? TauxRecouvrement => CalculsExecution.Taux(AnneeN, PrevuN);
    public decimal? Variation => AnneeN1 is { } n1 ? CalculsRecettes.TauxAccroissement(AnneeN, n1) : null;
}

/// <summary>Une échéance de recouvrement de l'exercice et sa couverture.</summary>
public sealed record SuiviEcheance(
    Ulid EcheanceId, Ulid PrevisionId, string Prevision, DateOnly Date, decimal Attendu, decimal Couvert, bool EnRetard)
{
    public decimal Reste => Attendu - Couvert;
    public string Statut => LibellesRecettes.StatutEcheance(Attendu, Couvert, EnRetard);
}

/// <summary>Un poste du bilan : prévu et réalisé.</summary>
public sealed record PosteBilan(string Libelle, decimal Prevu, decimal Realise)
{
    public decimal? Taux => CalculsExecution.Taux(Realise, Prevu);
}

/// <summary>
/// Bilan recettes / dépenses d'un exercice (cahier fonctionnel § 3.9.2 et 4.1). Les ressources
/// réalisées sont les encaissements validés et les reports de ressources antérieures ; les
/// dépenses réalisées, les paiements validés de l'exercice (tous axes, encours reportés compris).
/// </summary>
public sealed record BilanRecettesDepenses(
    int Annee, StatutExercice Statut, List<PosteBilan> Ressources, List<PosteBilan> Depenses, decimal DepensesEngagees,
    decimal Positionne, decimal NonPositionne)
{
    public decimal RessourcesPrevues => Ressources.Sum(r => r.Prevu);
    public decimal RessourcesRealisees => Ressources.Sum(r => r.Realise);
    public decimal DepensesPrevues => Depenses.Sum(d => d.Prevu);
    public decimal DepensesRealisees => Depenses.Sum(d => d.Realise);

    /// <summary>Équilibre du budget : ressources prévues − dotations totales.</summary>
    public decimal EquilibrePrevisionnel => RessourcesPrevues - DepensesPrevues;

    /// <summary>Bilan (§ 4.1) : recettes réalisées − dépenses réalisées.</summary>
    public decimal Bilan => CalculsRecettes.Bilan(RessourcesRealisees, DepensesRealisees);
}

/// <summary>
/// Vues de synthèse des recettes (cahier fonctionnel § 3.9, technique § 4.6) : ventilation par
/// catégorie, nature, site, collecteur ou ligne ; indicateurs clés ; évolution mensuelle ;
/// comparaison des sites sur trois exercices ; suivi de l'échéancier de recouvrement ;
/// bilan recettes / dépenses.
/// </summary>
public interface ISyntheseRecetteService
{
    Task<List<LigneSyntheseRecette>> RegrouperAsync(Ulid exerciceId, RegroupementRecette par);
    Task<IndicateursRecettes?> IndicateursAsync(Ulid exerciceId);
    Task<List<LigneMoisRecette>> MensuelAsync(Ulid exerciceId);
    Task<List<ComparaisonSiteRecette>> ComparaisonSitesAsync(Ulid exerciceId);
    Task<List<SuiviEcheance>> EcheancesAsync(Ulid exerciceId, bool enRetardSeulement = false);
    Task<BilanRecettesDepenses?> BilanAsync(Ulid exerciceId);
}

public sealed class SyntheseRecetteService : ISyntheseRecetteService
{
    private const string NonAffecte = "Non affecté";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesRecettes _acces;

    public SyntheseRecetteService(IDbContextFactory<AppDbContext> factory, AccesRecettes acces)
    {
        _factory = factory;
        _acces = acces;
    }

    public async Task<List<LigneSyntheseRecette>> RegrouperAsync(Ulid exerciceId, RegroupementRecette par)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var previsions = await PrevisionsAsync(ctx, exerciceId);

        // Les catégories gardent l'ordre de l'énumération (propres, taxes, additionnelles, externes).
        if (par == RegroupementRecette.Categorie)
            return previsions.GroupBy(p => p.Categorie).OrderBy(g => g.Key)
                .Select(g => new LigneSyntheseRecette(null, LibellesParametrage.De(LibellesParametrage.CategorieRecette, g.Key),
                    MontantsRecette.Somme(g.Select(p => p.Montants))))
                .ToList();

        IEnumerable<(Ulid? Cle, string Libelle, MontantsRecette Montants)> parts = par switch
        {
            RegroupementRecette.Nature => previsions.Select(p => ((Ulid?)p.NatureRecetteId, p.Nature, p.Montants)),
            RegroupementRecette.Site => previsions.Select(p => (p.SiteId, p.Site ?? NonAffecte, p.Montants)),
            RegroupementRecette.Collecteur => previsions.Select(p => (p.TiersCollecteurId, p.Collecteur ?? NonAffecte, p.Montants)),
            _ => previsions.Select(p => ((Ulid?)p.LigneBudgetaireId, p.LigneCode + " — " + p.LigneLibelle, p.Montants)),
        };
        var lignes = parts.GroupBy(p => p.Cle)
            .Select(g => new LigneSyntheseRecette(g.Key, g.First().Libelle, MontantsRecette.Somme(g.Select(p => p.Montants))))
            .ToList();

        // Par nature : en regard, la ressource programmée pour l'année dans le cycle de l'exercice (§ 3.3.5).
        if (par == RegroupementRecette.Nature)
        {
            var programmes = await ProgrammationAsync(ctx, exerciceId);
            var natures = await ctx.NaturesRecette.AsNoTracking().Where(n => programmes.Keys.Contains(n.Id))
                .ToDictionaryAsync(n => n.Id, n => n.Code + " — " + n.Libelle);
            lignes = lignes.Select(l => l with { Programme = l.Id is { } id && programmes.TryGetValue(id, out var m) ? m : null })
                .Concat(programmes.Where(p => lignes.All(l => l.Id != p.Key))
                    .Select(p => new LigneSyntheseRecette(p.Key, natures.GetValueOrDefault(p.Key) ?? NonAffecte, MontantsRecette.Zero, p.Value)))
                .ToList();
        }

        return lignes.OrderBy(l => l.Id is null).ThenBy(l => l.Libelle, StringComparer.CurrentCulture).ToList();
    }

    public async Task<IndicateursRecettes?> IndicateursAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => new { e.Annee, e.Statut }).FirstOrDefaultAsync();
        if (exercice is null) return null;

        var previsions = await PrevisionsAsync(ctx, exerciceId);
        var montants = MontantsRecette.Somme(previsions.Select(p => p.Montants));
        var additionnelles = previsions.Where(p => p.Categorie == CategorieRecette.RessourceAdditionnelle).Sum(p => p.Montants.Encaisse);
        var reports = await ctx.ReportsRessources.AsNoTracking().Where(r => r.ExerciceId == exerciceId)
            .GroupBy(r => r.EstPositionne).Select(g => new { g.Key, Montant = g.Sum(r => r.Montant) }).ToListAsync();

        // Période comparable : jusqu'au dernier mois encaissé de N (l'année entière une fois clôturé).
        var dernierMois = exercice.Statut == StatutExercice.Cloture ? 12
            : await ctx.Encaissements.Where(e => e.ExerciceId == exerciceId && e.Statut == StatutSaisie.Valide)
                .MaxAsync(e => (int?)e.Periode.Numero) ?? 0;
        var encaisseN1 = dernierMois == 0 ? 0 : await EncaisseAnneeAsync(ctx, exercice.Annee - 1, dernierMois) ?? 0;

        return new IndicateursRecettes(montants, reports.Sum(r => r.Montant), reports.Where(r => !r.Key).Sum(r => r.Montant),
            additionnelles, encaisseN1, dernierMois);
    }

    public async Task<List<LigneMoisRecette>> MensuelAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => new { e.Annee }).FirstOrDefaultAsync();
        if (exercice is null) return [];

        var mois = await ctx.Periodes.AsNoTracking().Where(p => p.ExerciceId == exerciceId && p.TypePeriode == TypePeriode.Mois)
            .OrderBy(p => p.Numero).Select(p => new { p.Numero, p.DateDebut }).ToListAsync();
        var n = await ParMoisAsync(ctx, ctx.Encaissements.Where(e => e.ExerciceId == exerciceId));
        var n1 = await ParMoisAsync(ctx, ctx.Encaissements.Where(e => e.Exercice.Annee == exercice.Annee - 1));
        var echeances = await ctx.EcheancesRecouvrement.AsNoTracking().Where(e => e.PrevisionRecette.ExerciceId == exerciceId)
            .Select(e => new { e.DateEcheance, e.MontantAttendu }).ToListAsync();

        return mois.Select(m => new LigneMoisRecette(m.Numero, m.DateDebut, n.GetValueOrDefault(m.Numero), n1.GetValueOrDefault(m.Numero),
            echeances.Where(e => e.DateEcheance < m.DateDebut.AddMonths(1)).Sum(e => e.MontantAttendu))).ToList();
    }

    public async Task<List<ComparaisonSiteRecette>> ComparaisonSitesAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var annee = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => (int?)e.Annee).FirstOrDefaultAsync();
        if (annee is not { } n) return [];

        var previsions = await PrevisionsAsync(ctx, exerciceId);
        var parAnnee = new Dictionary<int, Dictionary<Ulid, decimal>?>
        {
            [n - 1] = await EncaisseParSiteAsync(ctx, n - 1),
            [n - 2] = await EncaisseParSiteAsync(ctx, n - 2),
        };
        var sites = previsions.Where(p => p.SiteId is not null).Select(p => p.SiteId!.Value)
            .Concat(parAnnee.Values.OfType<Dictionary<Ulid, decimal>>().SelectMany(d => d.Keys))
            .Distinct().ToList();
        var noms = await ctx.Sites.AsNoTracking().Where(s => sites.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Nom);

        return sites
            .Select(id => new ComparaisonSiteRecette(id, noms.GetValueOrDefault(id) ?? NonAffecte,
                parAnnee[n - 2] is { } n2 ? n2.GetValueOrDefault(id) : null,
                parAnnee[n - 1] is { } n1 ? n1.GetValueOrDefault(id) : null,
                previsions.Where(p => p.SiteId == id).Sum(p => p.Montants.Encaisse),
                previsions.Where(p => p.SiteId == id).Sum(p => p.Montants.PrevisionRetenue)))
            .OrderBy(s => s.Site, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<List<SuiviEcheance>> EcheancesAsync(Ulid exerciceId, bool enRetardSeulement = false)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var suivi = await SuivreEcheancesAsync(ctx, ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.ExerciceId == exerciceId), PrevisionRecetteService.Aujourdhui);
        return (enRetardSeulement ? suivi.Where(e => e.EnRetard) : suivi).OrderBy(e => e.Date).ThenBy(e => e.Prevision).ToList();
    }

    public async Task<BilanRecettesDepenses?> BilanAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => new { e.Annee, e.Statut }).FirstOrDefaultAsync();
        if (exercice is null) return null;

        // Ressources : recettes de l'exercice par catégorie, puis reports de ressources antérieures.
        var previsions = await PrevisionsAsync(ctx, exerciceId);
        var ressources = previsions.GroupBy(p => p.Categorie).OrderBy(g => g.Key)
            .Select(g => new PosteBilan(LibellesParametrage.De(LibellesParametrage.CategorieRecette, g.Key),
                g.Sum(p => p.Montants.PrevisionRetenue), g.Sum(p => p.Montants.Encaisse)))
            .ToList();
        var reports = await ctx.ReportsRessources.AsNoTracking().Where(r => r.ExerciceId == exerciceId)
            .Select(r => new { r.Montant, r.EstPositionne }).ToListAsync();
        if (reports.Count > 0)
            ressources.Add(new PosteBilan(LibellesParametrage.De(LibellesParametrage.CategorieRecette, CategorieRecette.ReportRessourcesAnterieures),
                reports.Sum(r => r.Montant), reports.Sum(r => r.Montant)));

        // Dépenses : dotations de l'exercice par axe, situation du cycle commun.
        var dotations = await SituationService.Calculer(ctx, ctx.Dotations.AsNoTracking().Where(d => d.ExerciceId == exerciceId)).ToListAsync();
        var ordreAxes = await ctx.Axes.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Ordre);
        var depenses = dotations.GroupBy(d => (d.AxeId, d.Axe)).OrderBy(g => ordreAxes.GetValueOrDefault(g.Key.AxeId))
            .Select(g =>
            {
                var m = MontantsDotation.Somme(g.Select(d => d.Montants));
                return new PosteBilan(g.Key.Axe, m.DotationTotale, m.PaiementsTotal);
            })
            .ToList();

        var encaisse = MontantsRecette.Somme(previsions.Select(p => p.Montants));
        var positionne = encaisse.EncaissePositionne + reports.Where(r => r.EstPositionne).Sum(r => r.Montant);
        var nonPositionne = encaisse.EncaisseNonPositionne + reports.Where(r => !r.EstPositionne).Sum(r => r.Montant);
        return new BilanRecettesDepenses(exercice.Annee, exercice.Statut, ressources, depenses,
            MontantsDotation.Somme(dotations.Select(d => d.Montants)).AETotal, positionne, nonPositionne);
    }

    // ═════ Requêtes partagées ═════════════════════════════════════════════════

    private static Task<List<PrevisionCalculee>> PrevisionsAsync(AppDbContext ctx, Ulid exerciceId)
        => PrevisionRecetteService.Projeter(ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.ExerciceId == exerciceId)).ToListAsync();

    /// <summary>Ressources programmées pour l'année de l'exercice dans son cycle, par nature.</summary>
    private static async Task<Dictionary<Ulid, decimal>> ProgrammationAsync(AppDbContext ctx, Ulid exerciceId)
    {
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => new { e.Annee, e.CycleProgrammationId }).FirstAsync();
        if (exercice.CycleProgrammationId is not { } cycleId) return [];
        return await ctx.ProgrammationsRessources.AsNoTracking()
            .Where(r => r.CycleProgrammationId == cycleId && r.Annee == exercice.Annee)
            .GroupBy(r => r.NatureRecetteId)
            .Select(g => new { g.Key, Montant = g.Sum(r => r.Montant) })
            .ToDictionaryAsync(x => x.Key, x => x.Montant);
    }

    private static async Task<Dictionary<int, decimal>> ParMoisAsync(AppDbContext ctx, IQueryable<Core.Entities.Encaissement> q)
        => await q.AsNoTracking().Where(e => e.Statut == StatutSaisie.Valide)
            .GroupBy(e => e.Periode.Numero).Select(g => new { g.Key, Montant = g.Sum(e => e.Montant) })
            .ToDictionaryAsync(x => x.Key, x => x.Montant);

    /// <summary>
    /// Encaissé d'une année jusqu'à un mois donné : les encaissements de son exercice s'il en
    /// porte, sinon l'historique repris (année entière seulement). null sans aucune donnée.
    /// </summary>
    private static async Task<decimal?> EncaisseAnneeAsync(AppDbContext ctx, int annee, int jusquAuMois)
    {
        var q = ctx.Encaissements.AsNoTracking().Where(e => e.Exercice.Annee == annee && e.Statut == StatutSaisie.Valide);
        if (await q.AnyAsync()) return await q.Where(e => e.Periode.Numero <= jusquAuMois).SumAsync(e => (decimal?)e.Montant) ?? 0;
        if (jusquAuMois < 12) return null;
        return await ctx.ExecutionsHistoriques.AsNoTracking().Where(h => h.Annee == annee && h.NatureRecetteId != null)
            .SumAsync(h => h.MontantRealise);
    }

    /// <summary>Encaissé d'une année par site : encaissements de l'exercice, sinon historique. null sans aucune donnée.</summary>
    private static async Task<Dictionary<Ulid, decimal>?> EncaisseParSiteAsync(AppDbContext ctx, int annee)
    {
        var q = ctx.Encaissements.AsNoTracking().Where(e => e.Exercice.Annee == annee && e.Statut == StatutSaisie.Valide);
        if (await q.AnyAsync())
            return await q.Where(e => e.PrevisionRecette.SiteId != null).GroupBy(e => e.PrevisionRecette.SiteId!.Value)
                .Select(g => new { g.Key, Montant = g.Sum(e => e.Montant) }).ToDictionaryAsync(x => x.Key, x => x.Montant);
        var historique = ctx.ExecutionsHistoriques.AsNoTracking().Where(h => h.Annee == annee && h.NatureRecetteId != null);
        if (!await historique.AnyAsync()) return null;
        return await historique.Where(h => h.SiteId != null).GroupBy(h => h.SiteId!.Value)
            .Select(g => new { g.Key, Montant = g.Sum(h => h.MontantRealise ?? 0) }).ToDictionaryAsync(x => x.Key, x => x.Montant);
    }

    /// <summary>Échéances des prévisions et leur couverture par les encaissements validés.</summary>
    internal static async Task<List<SuiviEcheance>> SuivreEcheancesAsync(AppDbContext ctx, IQueryable<Core.Entities.PrevisionRecette> previsions, DateOnly aujourdhui)
    {
        var lignes = await previsions.Where(p => p.Echeances.Any())
            .Select(p => new
            {
                p.Id,
                Libelle = p.LigneBudgetaire.Code + " — " + p.NatureRecette.Libelle + (p.Site != null ? " · " + p.Site.Nom : ""),
                Encaisse = p.Encaissements.Where(e => e.Statut == StatutSaisie.Valide).Sum(e => (decimal?)e.Montant) ?? 0,
                Echeances = p.Echeances.Select(e => new { e.Id, e.DateEcheance, e.MontantAttendu }).ToList()
            })
            .ToListAsync();

        return lignes.SelectMany(p =>
        {
            var ordonnees = p.Echeances.OrderBy(e => e.DateEcheance).ToList();
            var couverture = CalculsRecettes.Couverture(ordonnees.Select(e => (e.DateEcheance, e.MontantAttendu)), p.Encaisse, aujourdhui);
            return ordonnees.Select((e, i) => new SuiviEcheance(e.Id, p.Id, p.Libelle, e.DateEcheance, e.MontantAttendu, couverture[i].Couvert, couverture[i].EnRetard));
        }).ToList();
    }
}
