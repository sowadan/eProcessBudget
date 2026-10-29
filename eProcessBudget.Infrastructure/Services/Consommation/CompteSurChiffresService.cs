using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.BudgetProgramme;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Infrastructure.Services.Recettes;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Consommation;

/// <summary>Une ligne de recette du compte sur chiffres (ligne budgétaire ou nature de report).</summary>
public sealed record LigneChiffresRecette(string Code, string Libelle, Section Section, CategorieRecette Categorie, ChiffresRecette Chiffres);

/// <summary>Une ligne de dépense du compte sur chiffres : un chapitre (rubrique), ou les engagements antérieurs reportés.</summary>
public sealed record LigneChiffresDepense(string Code, string Libelle, Section Section, ChiffresDepense Chiffres);

public sealed record CompteChiffresRecettes(int Annee, List<LigneChiffresRecette> Lignes, List<LigneChiffresRecette> Reports)
{
    public ChiffresRecette Total(Section section) => ChiffresRecette.Somme(Lignes.Where(l => l.Section == section).Select(l => l.Chiffres));
    public ChiffresRecette TotalBudgetaire => ChiffresRecette.Somme(Lignes.Select(l => l.Chiffres));
    public ChiffresRecette TotalReports => ChiffresRecette.Somme(Reports.Select(l => l.Chiffres));
    public ChiffresRecette TotalGeneral => TotalBudgetaire + TotalReports;
}

public sealed record CompteChiffresDepenses(int Annee, List<LigneChiffresDepense> Chapitres, ChiffresDepense ReportsAnterieurs, ChiffresDepense DontEntretien)
{
    public ChiffresDepense Total(Section section) => ChiffresDepense.Somme(Chapitres.Where(l => l.Section == section).Select(l => l.Chiffres));
    public ChiffresDepense TotalBudgetaire => ChiffresDepense.Somme(Chapitres.Select(l => l.Chiffres));
    public ChiffresDepense TotalGeneral => TotalBudgetaire + ReportsAnterieurs;
}

/// <summary>Une ligne de l'état de présentation : prévisions et réalisations des deux exercices.</summary>
public sealed record LignePresentation(
    string Sens, string Code, string Libelle, decimal PrevisionN1, decimal RealiseN1, decimal PrevisionN, string? Commentaire)
{
    public decimal Variation => PrevisionN - PrevisionN1;
    public decimal? TauxVariation => CalculsPresentation.TauxVariation(PrevisionN, PrevisionN1);
    public bool Significative => CalculsPresentation.EstSignificative(PrevisionN, PrevisionN1);
}

public sealed record EtatPresentation(int Annee, int AnneePrecedente, List<LignePresentation> Recettes, List<LignePresentation> Depenses);

/// <summary>
/// Compte sur chiffres et états de fin d'exercice transmis par le métier (réponses C.2.2,
/// C.2.7 et C.2.9) :
/// <list type="bullet">
/// <item>recettes par ligne : prévision ajustée, factures émises (% d'exécution), recouvrées
/// (% de recouvrement), reste à recouvrer ; puis les reports de ressources (521, 409, 4495, 411) ;</item>
/// <item>dépenses par chapitre (rubrique), section par section : prévision ajustée, prise en
/// charge (factures reçues, % d'exécution), rejets ou pénalités, paiements (% de paiement),
/// reste à payer ; puis les engagements antérieurs reportés ;</item>
/// <item>équilibre de trésorerie A à I, base des reports de l'exercice suivant ;</item>
/// <item>état de présentation du budget : lignes des deux exercices, variations et commentaires
/// par rubrique quand la variation est significative.</item>
/// </list>
/// Recettes : droit de lecture des recettes. Dépenses : consommation budgétaire, restreinte au
/// périmètre de lecture. Équilibre : les deux.
/// </summary>
public interface ICompteSurChiffresService
{
    Task<CompteChiffresRecettes?> RecettesAsync(Ulid exerciceId);
    Task<CompteChiffresDepenses?> DepensesAsync(Ulid exerciceId);
    Task<EquilibreTresorerie?> EquilibreAsync(Ulid exerciceId);

    /// <summary>Exercice et précédent ; la partie recettes est vide sans droit de lecture des recettes.</summary>
    Task<EtatPresentation?> PresentationAsync(Ulid exerciceId);
}

public sealed class CompteSurChiffresService : ICompteSurChiffresService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesConsommation _acces;
    private readonly AccesRecettes _accesRecettes;

    public CompteSurChiffresService(IDbContextFactory<AppDbContext> factory, AccesConsommation acces, AccesRecettes accesRecettes)
    {
        _factory = factory;
        _acces = acces;
        _accesRecettes = accesRecettes;
    }

    public async Task<CompteChiffresRecettes?> RecettesAsync(Ulid exerciceId)
    {
        if (await _accesRecettes.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await CalculerRecettesAsync(ctx, exerciceId);
    }

    public async Task<CompteChiffresDepenses?> DepensesAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync(TypeDroit.ConsommationLire) is not { } lecteur) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await CalculerDepensesAsync(ctx, exerciceId, lecteur.Perimetre);
    }

    public async Task<EquilibreTresorerie?> EquilibreAsync(Ulid exerciceId)
    {
        if (await _accesRecettes.LecteurAsync() == Ulid.Empty) return null;
        if (await _acces.LecteurAsync(TypeDroit.ConsommationLire) is not { } lecteur) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var recettes = await CalculerRecettesAsync(ctx, exerciceId);
        var depenses = await CalculerDepensesAsync(ctx, exerciceId, lecteur.Perimetre);
        return recettes is null || depenses is null ? null : Equilibre(recettes, depenses);
    }

    public async Task<EtatPresentation?> PresentationAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync(TypeDroit.ConsommationLire) is not { } lecteur) return null;
        var recettesVisibles = await _accesRecettes.LecteurAsync() != Ulid.Empty;
        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => new { e.Annee }).FirstOrDefaultAsync();
        if (exercice is null) return null;
        var precedentId = await ctx.Exercices.AsNoTracking().Where(e => e.Annee == exercice.Annee - 1).Select(e => (Ulid?)e.Id).FirstOrDefaultAsync();

        // Commentaires par rubrique de l'exercice présenté (analyses « commentaire de rubrique »), par code de chapitre.
        var commentaires = (await ctx.AnalysesQualitatives.AsNoTracking()
                .Where(a => a.ExerciceId == exerciceId && a.TypeAnalyse == TypeAnalyse.CommentaireRubrique && a.Rubrique != null)
                .OrderBy(a => a.Ordre)
                .Select(a => new { a.Rubrique!.Code, a.Contenu }).ToListAsync())
            .GroupBy(a => a.Code).ToDictionary(g => g.Key, g => string.Join(" ", g.Select(a => a.Contenu)));

        var depensesN = await CalculerDepensesAsync(ctx, exerciceId, lecteur.Perimetre);
        var depensesN1 = precedentId is { } p ? await CalculerDepensesAsync(ctx, p, lecteur.Perimetre) : null;
        var depenses = Comparer(
            depensesN!.Chapitres.Select(c => (c.Code, c.Libelle, c.Chiffres.Prevision, c.Chiffres.Paiements)),
            depensesN1?.Chapitres.Select(c => (c.Code, c.Libelle, c.Chiffres.Prevision, c.Chiffres.Paiements)) ?? [],
            "Dépenses", commentaires);

        var recettes = new List<LignePresentation>();
        if (recettesVisibles)
        {
            var recettesN = await CalculerRecettesAsync(ctx, exerciceId);
            var recettesN1 = precedentId is { } q ? await CalculerRecettesAsync(ctx, q) : null;
            static IEnumerable<(string, string, decimal, decimal)> Lignes(CompteChiffresRecettes? c)
                => c is null ? [] : c.Lignes.Concat(c.Reports).Select(l => (l.Code, l.Libelle, l.Chiffres.Prevision, l.Chiffres.Recouvre));
            recettes = Comparer(Lignes(recettesN), Lignes(recettesN1), "Recettes", commentaires);
        }
        return new EtatPresentation(exercice.Annee, exercice.Annee - 1, recettes, depenses);
    }

    private static List<LignePresentation> Comparer(
        IEnumerable<(string Code, string Libelle, decimal Prevision, decimal Realise)> n,
        IEnumerable<(string Code, string Libelle, decimal Prevision, decimal Realise)> n1,
        string sens, IReadOnlyDictionary<string, string> commentaires)
    {
        var courant = n.ToList();
        var precedent = n1.ToList();
        return courant.Select(l => l.Code).Concat(precedent.Select(l => l.Code)).Distinct()
            .Select(code =>
            {
                var c = courant.Where(l => l.Code == code).ToList();
                var p = precedent.Where(l => l.Code == code).ToList();
                var libelle = c.Concat(p).First().Libelle;
                return new LignePresentation(sens, code, libelle, p.Sum(l => l.Prevision), p.Sum(l => l.Realise), c.Sum(l => l.Prevision),
                    commentaires.GetValueOrDefault(code));
            })
            .OrderBy(l => l.Code, StringComparer.Ordinal)
            .ToList();
    }

    internal static EquilibreTresorerie Equilibre(CompteChiffresRecettes r, CompteChiffresDepenses d)
    {
        var general = r.TotalGeneral;
        var depenses = d.TotalGeneral;
        var taxes = ChiffresRecette.Somme(r.Lignes.Where(l => l.Categorie == CategorieRecette.TaxeAffectee).Select(l => l.Chiffres));
        var autres = ChiffresRecette.Somme(r.Lignes.Where(l => l.Categorie != CategorieRecette.TaxeAffectee).Select(l => l.Chiffres))
                     + r.TotalReports;
        return new EquilibreTresorerie(general.Recouvre, depenses.Paiements + depenses.Rejets, depenses.ResteAPayer,
            taxes.ResteARecouvrer, autres.ResteARecouvrer, general.Emis, depenses.PriseEnCharge);
    }

    // ═════ Recettes ═══════════════════════════════════════════════════════════

    internal static async Task<CompteChiffresRecettes?> CalculerRecettesAsync(AppDbContext ctx, Ulid exerciceId)
    {
        var annee = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => (int?)e.Annee).FirstOrDefaultAsync();
        if (annee is null) return null;

        var previsions = await PrevisionRecetteService.Projeter(ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.ExerciceId == exerciceId)).ToListAsync();
        var sections = await ctx.Axes.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Section);
        var rubriques = await ctx.Rubriques.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Code);
        var lignes = previsions
            .GroupBy(p => (p.LigneBudgetaireId, p.LigneCode, p.LigneLibelle))
            .Select(g =>
            {
                var premiere = g.First();
                // Catégorie dominante de la ligne (en montant prévu) : sert à ventiler le reste à recouvrer (F / G).
                var categorie = g.GroupBy(p => p.Categorie).OrderByDescending(c => c.Sum(p => p.Montants.PrevisionRetenue)).First().Key;
                return (Rubrique: rubriques.GetValueOrDefault(premiere.RubriqueId) ?? "",
                        Ligne: new LigneChiffresRecette(g.Key.LigneCode, g.Key.LigneLibelle, sections.GetValueOrDefault(premiere.AxeId, Section.Fonctionnement), categorie,
                            ChiffresRecette.Somme(g.Select(p => new ChiffresRecette(p.Montants.PrevisionRetenue, p.Montants.Emis, p.Montants.Encaisse)))));
            })
            .OrderBy(x => x.Rubrique, StringComparer.Ordinal).ThenBy(x => x.Ligne.Code, StringComparer.Ordinal)
            .Select(x => x.Ligne)
            .ToList();

        // Reports de ressources : constatés à la clôture précédente, donc émis = recouvrés.
        var reports = (await ctx.ReportsRessources.AsNoTracking().Where(r => r.ExerciceId == exerciceId)
                .Select(r => new { r.NatureRecette.Code, r.NatureRecette.Libelle, r.NatureRecette.CategorieRecette, r.Montant, r.MontantPrevu })
                .ToListAsync())
            .GroupBy(r => (r.Code, r.Libelle, r.CategorieRecette))
            .OrderBy(g => g.Key.Code, StringComparer.Ordinal)
            .Select(g => new LigneChiffresRecette(g.Key.Code, g.Key.Libelle, Section.HorsSections, g.Key.CategorieRecette,
                new ChiffresRecette(g.Sum(r => r.MontantPrevu ?? 0), g.Sum(r => r.Montant), g.Sum(r => r.Montant))))
            .ToList();
        return new CompteChiffresRecettes(annee.Value, lignes, reports);
    }

    // ═════ Dépenses ═══════════════════════════════════════════════════════════

    internal static async Task<CompteChiffresDepenses?> CalculerDepensesAsync(AppDbContext ctx, Ulid exerciceId, PerimetreLecture perimetre)
    {
        var annee = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId).Select(e => (int?)e.Annee).FirstOrDefaultAsync();
        if (annee is null) return null;

        var source = Consolidation.Source(ctx, new FiltreConsommation { ExerciceId = exerciceId }, perimetre);
        var dotations = await SituationService.Calculer(ctx, source).ToListAsync();
        var ids = dotations.Select(d => d.Id).ToList();

        // Prise en charge : factures reçues sur les AD de l'exercice (validées) ; rejets par nature d'engagement.
        var factures = await ctx.Factures.AsNoTracking()
            .Where(f => ids.Contains(f.Engagement.DotationId) && f.Statut == StatutSaisie.Valide
                     && f.Engagement.Statut == StatutSaisie.Valide && !f.Engagement.EstReport)
            .GroupBy(f => f.Engagement.DotationId).Select(g => new { g.Key, Montant = g.Sum(f => f.Montant) })
            .ToDictionaryAsync(x => x.Key, x => x.Montant);
        var rejets = await ctx.RejetsPenalites.AsNoTracking()
            .Where(r => ids.Contains(r.Engagement.DotationId) && r.Engagement.Statut == StatutSaisie.Valide)
            .GroupBy(r => new { r.Engagement.DotationId, r.Engagement.EstReport }).Select(g => new { g.Key.DotationId, g.Key.EstReport, Montant = g.Sum(r => r.Montant) })
            .ToListAsync();
        decimal Rejets(Ulid dotationId, bool report) => rejets.Where(r => r.DotationId == dotationId && r.EstReport == report).Sum(r => r.Montant);

        var rubriques = await ctx.Rubriques.AsNoTracking().ToDictionaryAsync(r => r.Id, r => new { r.Code, r.Libelle });
        var sections = await ctx.Axes.AsNoTracking().ToDictionaryAsync(a => a.Id, a => new { a.Section, a.Code });
        var financementEntretien = (await BesoinEntretienService.Financement(source).Select(d => d.Id).ToListAsync()).ToHashSet();

        ChiffresDepense Courant(DotationCalculee d) => new(d.Montants.PrevisionAjustee, factures.GetValueOrDefault(d.Id), Rejets(d.Id, false), d.Montants.PaiementsPeriode);
        ChiffresDepense Reporte(DotationCalculee d) => new(d.Montants.Reports, d.Montants.AEReports, Rejets(d.Id, true), d.Montants.PaiementsTotal - d.Montants.PaiementsPeriode);

        var chapitres = dotations
            .GroupBy(d => rubriques.TryGetValue(d.RubriqueId, out var r) ? r.Code : "")
            .Select(g =>
            {
                var premiere = g.First();
                var libelle = rubriques.TryGetValue(premiere.RubriqueId, out var r) ? r.Libelle : premiere.Rubrique;
                // Section : celle de l'axe majoritaire du chapitre (en prévision).
                var section = g.GroupBy(d => sections.TryGetValue(d.AxeId, out var a) ? a.Section : Section.Fonctionnement)
                    .OrderByDescending(s => s.Sum(d => d.Montants.PrevisionAjustee)).First().Key;
                return new LigneChiffresDepense(g.Key, libelle, section, ChiffresDepense.Somme(g.Select(Courant)));
            })
            .OrderBy(c => c.Section).ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList();
        var reports = ChiffresDepense.Somme(dotations.Select(Reporte));
        var entretien = ChiffresDepense.Somme(dotations.Where(d => financementEntretien.Contains(d.Id)).Select(d => Courant(d) + Reporte(d)));
        return new CompteChiffresDepenses(annee.Value, chapitres, reports, entretien);
    }
}
