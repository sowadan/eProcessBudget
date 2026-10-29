using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Consommation;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>Critère de regroupement de la situation d'un axe.</summary>
public enum Regroupement
{
    Projet = 1,
    Site = 2,
    Structure = 3,
    Entite = 4,
    Rubrique = 5,
    Nature = 6,

    /// <summary>Par activité budgétaire, selon les clés de répartition des lignes (§ 3.3.5).</summary>
    Activite = 7,

    /// <summary>Par action, puis par programme, selon les mêmes clés.</summary>
    Action = 8,
    Programme = 9,

    Axe = 10,
    SousRubrique = 11,
    Ligne = 12,

    /// <summary>Source de financement (RI / RE).</summary>
    Source = 13,

    /// <summary>Compte comptable principal de la ligne (plan comptable).</summary>
    Compte = 14,
}

/// <summary>Situation cumulée d'un regroupement (un site, un service, une activité...).</summary>
/// <param name="Id">null pour les dotations sans cet élément (« non affecté »).</param>
public sealed record LigneRegroupement(Ulid? Id, string Libelle, MontantsDotation Montants, int NbDotations)
{
    /// <summary>Solde (§ 4.1, solde des appuis) : montant prévu (dotation totale) − montant versé.</summary>
    public decimal Solde => CalculsAxes.SoldeAppui(Montants.DotationTotale, Montants.PaiementsTotal);
}

/// <summary>Une dotation proche du dépassement : le seuil d'alerte le plus haut qu'elle atteint.</summary>
public sealed record LigneSousTension(LigneDotation Dotation, decimal Seuil);

/// <summary>Une entité bénéficiaire d'appuis et sa situation (§ 3.8).</summary>
public sealed record LigneAppuiEntite(
    Ulid? Id, string Entite, string? Categorie, string? Parent, string? Sites, string? CompteGeneral, MontantsDotation Montants, int NbEnAttente)
{
    public decimal Solde => CalculsAxes.SoldeAppui(Montants.DotationTotale, Montants.PaiementsTotal);
}

/// <summary>Engagement d'appui validé et pas entièrement versé.</summary>
public sealed record LigneAppuiEnAttente(
    Ulid EngagementId, string Numero, string Objet, string? Entite, string? Projet, string LigneCode,
    DateOnly Date, DateTime? DateValidation, decimal Montant, decimal Verse, decimal Rejets)
{
    public decimal Reste => CalculsExecution.ResteAPayer(Montant, Verse, Rejets);

    /// <summary>Jours écoulés depuis la validation.</summary>
    public int JoursAttente(DateTime maintenant) => DateValidation is { } v ? Math.Max(0, (int)(maintenant - v).TotalDays) : 0;
}

/// <summary>Critères de l'historique des versements d'appuis.</summary>
public sealed class FiltreVersements
{
    public Ulid ExerciceId { get; set; }

    /// <summary>Entité bénéficiaire ; ses sous-entités sont comprises.</summary>
    public Ulid? TiersId { get; set; }

    public Ulid? ProjetId { get; set; }
    public DateOnly? Du { get; set; }
    public DateOnly? Au { get; set; }
    public string? Recherche { get; set; }
}

/// <summary>Effectif d'une catégorie d'emplois : programmé sur l'année, agents actifs au référentiel.</summary>
public sealed record LigneEffectif(string Categorie, int EffectifProgramme, int AgentsActifs);

/// <summary>Dépenses de personnel de l'axe Fonctionnement mises en regard des effectifs (§ 3.7.2, 3.3.6).</summary>
public sealed record PersonnelEffectifs(MontantsDotation Montants, string? Cycle, List<LigneEffectif> Effectifs)
{
    public int TotalProgramme => Effectifs.Sum(e => e.EffectifProgramme);
    public int TotalAgents => Effectifs.Sum(e => e.AgentsActifs);

    /// <summary>Dotation de personnel par emploi programmé ; null sans effectif programmé.</summary>
    public decimal? DotationParEmploi => TotalProgramme == 0 ? null : Math.Round(Montants.DotationTotale / TotalProgramme, 2);

    /// <summary>Paiements de personnel par agent actif ; null sans agent.</summary>
    public decimal? PayeParAgent => TotalAgents == 0 ? null : Math.Round(Montants.PaiementsTotal / TotalAgents, 2);
}

/// <summary>
/// Vues de synthèse des axes de dépenses (cahier fonctionnel § 3.5 à 3.8, technique § 4.5) :
/// situation d'un axe regroupée par projet, site, service, entité, rubrique, nature de
/// dépense ou activité ; comparaison inter-sites ; dépenses de personnel et effectifs ;
/// appuis par entité, versements et appuis en attente de versement.
///
/// Tout part des dotations de l'axe et des formules du cycle commun : rien n'est ressaisi.
/// </summary>
public interface ISyntheseAxeService
{
    Task<MontantsDotation> TotauxAsync(AxeDepense axe, Ulid exerciceId);

    /// <summary>Dotations de l'axe et leur situation (le filtre d'axe est imposé).</summary>
    Task<(List<LigneDotation> Items, int Total)> ListerDotationsAsync(AxeDepense axe, FiltreDotations filtre, int page = 1, int taille = 20);

    Task<List<LigneRegroupement>> RegrouperAsync(AxeDepense axe, Ulid exerciceId, Regroupement par);

    /// <summary>
    /// Dotations de l'axe dont le taux d'engagement (y compris reports) atteint au moins le
    /// plus bas de leurs seuils d'alerte (§ 3.7.2), de la plus engagée à la moins engagée.
    /// </summary>
    Task<List<LigneSousTension>> ProchesDepassementAsync(AxeDepense axe, Ulid exerciceId);

    Task<PersonnelEffectifs?> PersonnelAsync(Ulid exerciceId);

    Task<List<LigneAppuiEntite>> AppuisParEntiteAsync(Ulid exerciceId);
    Task<List<LigneAppuiEnAttente>> AppuisEnAttenteAsync(Ulid exerciceId);
    Task<(List<LignePaiement> Items, int Total, decimal Montant)> VersementsAsync(FiltreVersements filtre, int page = 1, int taille = 20);
}

public sealed class SyntheseAxeService : ISyntheseAxeService
{
    /// <summary>Code de la nature de dépense « Personnel » (données initiales, cahier technique § 3.7).</summary>
    public const string CodeNaturePersonnel = "1";

    private const string NonAffecte = "Non affecté";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesAxes _acces;

    public SyntheseAxeService(IDbContextFactory<AppDbContext> factory, AccesAxes acces)
    {
        _factory = factory;
        _acces = acces;
    }

    public async Task<MontantsDotation> TotauxAsync(AxeDepense axe, Ulid exerciceId)
    {
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return MontantsDotation.Zero;
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return MontantsDotation.Zero;
        var lignes = await SituationService.Calculer(ctx, RequetesAxes.DotationsAxe(ctx, axeId, exerciceId)).ToListAsync();
        return MontantsDotation.Somme(lignes.Select(l => l.Montants));
    }

    public async Task<(List<LigneDotation> Items, int Total)> ListerDotationsAsync(AxeDepense axe, FiltreDotations f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return ([], 0);
        f.AxeId = axeId;
        var source = SituationService.Filtrer(ctx.Dotations.AsNoTracking(), f);
        var total = await source.CountAsync();
        var lignes = await SituationService.Calculer(ctx, source.OrderBy(d => d.LigneBudgetaire.Code).ThenBy(d => d.Id)
                .Skip((page - 1) * taille).Take(taille))
            .ToListAsync();
        return (lignes.Select(l => l.VersLigne()).ToList(), total);
    }

    public async Task<List<LigneRegroupement>> RegrouperAsync(AxeDepense axe, Ulid exerciceId, Regroupement par)
    {
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return [];

        var dotations = await SituationService.Calculer(ctx, RequetesAxes.DotationsAxe(ctx, axeId, exerciceId)).ToListAsync();
        return await Consolidation.RegrouperAsync(ctx, exerciceId, dotations.Select(d => new DotationRetenue(d, d.Montants)).ToList(), par);
    }

    public async Task<List<LigneSousTension>> ProchesDepassementAsync(AxeDepense axe, Ulid exerciceId)
    {
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return [];

        var seuils = await AlerteService.LireSeuilsAsync(ctx);
        if (seuils.Count == 0) return [];
        var dotations = await SituationService.Calculer(ctx, RequetesAxes.DotationsAxe(ctx, axeId, exerciceId)).ToListAsync();
        return dotations
            .Select(d => (Dotation: d, Seuil: CalculsExecution.SeuilAtteint(d.Montants.TauxEngagementYCReports,
                                                                            AlerteService.SeuilsApplicables(seuils, d.LigneBudgetaireId, d.AxeId))))
            .Where(x => x.Seuil is not null)
            .OrderByDescending(x => x.Dotation.Montants.TauxEngagementYCReports)
            .Select(x => new LigneSousTension(x.Dotation.VersLigne(), x.Seuil!.Value))
            .ToList();
    }

    public async Task<PersonnelEffectifs?> PersonnelAsync(Ulid exerciceId)
    {
        var axe = AxesDepense.Fonctionnement;
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == exerciceId)
            .Select(e => new { e.Annee, e.CycleProgrammationId, Cycle = e.CycleProgrammation != null ? e.CycleProgrammation.Libelle : null })
            .FirstOrDefaultAsync();
        if (exercice is null) return null;

        var montants = MontantsDotation.Zero;
        if (await AccesAxes.AxeIdAsync(ctx, axe) is { } axeId)
        {
            var lignes = await SituationService.Calculer(ctx, RequetesAxes.DotationsAxe(ctx, axeId, exerciceId)
                    .Where(d => d.LigneBudgetaire.NatureDepense != null && d.LigneBudgetaire.NatureDepense.Code == CodeNaturePersonnel))
                .ToListAsync();
            montants = MontantsDotation.Somme(lignes.Select(l => l.Montants));
        }

        // Effectifs programmés de l'année, dans le cycle rattaché à l'exercice (§ 3.3.6).
        var programmes = exercice.CycleProgrammationId is { } cycleId
            ? await ctx.ProgrammationsEmplois.AsNoTracking()
                .Where(p => p.CycleProgrammationId == cycleId && p.Annee == exercice.Annee)
                .GroupBy(p => p.CategorieEmploiId)
                .Select(g => new { g.Key, Effectif = g.Sum(p => p.Effectif) })
                .ToDictionaryAsync(x => x.Key, x => x.Effectif)
            : [];
        var agents = await ctx.Agents.AsNoTracking().Where(a => a.EstActif)
            .GroupBy(a => a.CategorieEmploiId)
            .Select(g => new { g.Key, Nombre = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Nombre);
        var categories = await ctx.CategoriesEmploi.AsNoTracking()
            .OrderBy(c => c.Ordre).ThenBy(c => c.Code)
            .Select(c => new { c.Id, c.Code, c.Libelle, c.EstActif })
            .ToListAsync();

        var effectifs = categories
            .Where(c => c.EstActif || programmes.ContainsKey(c.Id) || agents.ContainsKey(c.Id))
            .Select(c => new LigneEffectif($"{c.Code} — {c.Libelle}", programmes.GetValueOrDefault(c.Id), agents.GetValueOrDefault(c.Id)))
            .ToList();
        return new PersonnelEffectifs(montants, exercice.Cycle, effectifs);
    }

    public async Task<List<LigneAppuiEntite>> AppuisParEntiteAsync(Ulid exerciceId)
    {
        var axe = AxesDepense.Appuis;
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return [];

        var dotations = await SituationService.Calculer(ctx, RequetesAxes.DotationsAxe(ctx, axeId, exerciceId)).ToListAsync();
        var entites = await Consolidation.EntitesAsync(ctx, dotations);
        var enAttente = (await EnAttenteAsync(ctx, axeId, exerciceId)).ToLookup(a => a.EntiteId);

        var ids = entites.Values.Concat(enAttente.Select(g => g.Key)).OfType<Ulid>().Distinct().ToList();
        var tiers = await ctx.Tiers.AsNoTracking().Where(t => ids.Contains(t.Id))
            .Select(t => new
            {
                t.Id, L = t.Code + " — " + t.RaisonSociale,
                Categorie = t.CategorieEntite != null ? t.CategorieEntite.Libelle : null,
                Parent = t.TiersParent != null ? t.TiersParent.RaisonSociale : null,
                Compte = t.CompteGeneral != null ? t.CompteGeneral.Numero : null
            })
            .ToDictionaryAsync(t => t.Id);
        // Collectivités : sites (postes de péage) de leur zone d'implantation (C.2.8).
        var sites = (await ctx.SitesTiers.AsNoTracking().Where(st => ids.Contains(st.TiersId))
                .Select(st => new { st.TiersId, st.Site.Nom }).ToListAsync())
            .ToLookup(x => x.TiersId, x => x.Nom);

        return ids.Cast<Ulid?>().Append(null)
            .Select(id =>
            {
                var lignes = dotations.Where(d => entites[d.Id] == id).ToList();
                var nbEnAttente = enAttente[id].Count();
                if (lignes.Count == 0 && nbEnAttente == 0) return null;
                var t = id is { } i ? tiers.GetValueOrDefault(i) : null;
                return new LigneAppuiEntite(id, t?.L ?? NonAffecte, t?.Categorie, t?.Parent,
                    id is { } s && sites[s].Any() ? string.Join(", ", sites[s].Order()) : null, t?.Compte,
                    MontantsDotation.Somme(lignes.Select(l => l.Montants)), nbEnAttente);
            })
            .OfType<LigneAppuiEntite>()
            .OrderBy(l => l.Id is null).ThenBy(l => l.Parent ?? l.Entite, StringComparer.CurrentCulture).ThenBy(l => l.Entite, StringComparer.CurrentCulture)
            .ToList();
    }

    public async Task<List<LigneAppuiEnAttente>> AppuisEnAttenteAsync(Ulid exerciceId)
    {
        var axe = AxesDepense.Appuis;
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return [];
        return (await EnAttenteAsync(ctx, axeId, exerciceId)).Select(a => a.Ligne).ToList();
    }

    public async Task<(List<LignePaiement> Items, int Total, decimal Montant)> VersementsAsync(FiltreVersements f, int page = 1, int taille = 20)
    {
        var axe = AxesDepense.Appuis;
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return ([], 0, 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return ([], 0, 0);
        var q = ctx.Paiements.AsNoTracking().Where(p => p.ExerciceId == f.ExerciceId && p.Statut == StatutSaisie.Valide
                                                     && p.Engagement.Dotation.LigneBudgetaire.SousRubrique.Rubrique.AxeId == axeId);
        if (f.Du is { } du) q = q.Where(p => p.DatePaiement >= du);
        if (f.Au is { } au) q = q.Where(p => p.DatePaiement <= au);
        if (f.ProjetId is { } projet) q = q.Where(p => p.Engagement.Dotation.ProjetId == projet);
        if (f.TiersId is { } tiers)
        {
            // L'entité, ou l'une de ses sous-entités (C.2.8), qu'elle soit portée par l'engagement, la dotation ou le projet.
            var entites = await ctx.Tiers.AsNoTracking().Where(t => t.Id == tiers || t.TiersParentId == tiers).Select(t => t.Id).ToListAsync();
            q = q.Where(p => (p.Engagement.TiersId != null && entites.Contains(p.Engagement.TiersId.Value))
                          || (p.Engagement.Dotation.TiersId != null && entites.Contains(p.Engagement.Dotation.TiersId.Value))
                          || (p.Engagement.Dotation.Projet != null && p.Engagement.Dotation.Projet.TiersBeneficiaireId != null
                              && entites.Contains(p.Engagement.Dotation.Projet.TiersBeneficiaireId.Value)));
        }
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(p => p.NumeroPiece.Contains(r) || p.Engagement.Numero.Contains(r) || p.Engagement.Objet.Contains(r));
        }

        var total = await q.CountAsync();
        var montant = await q.SumAsync(p => (decimal?)p.Montant) ?? 0;
        var items = await PaiementService.ProjeterPaiements(q.OrderByDescending(p => p.DatePaiement).ThenByDescending(p => p.NumeroPiece)
            .Skip((page - 1) * taille).Take(taille)).ToListAsync();
        return (items, total, montant);
    }

    // ═════ Outils ═════════════════════════════════════════════════════════════

    /// <summary>Engagements d'appui validés de l'exercice, au reste à verser positif, du plus ancien au plus récent.</summary>
    internal static async Task<List<(Ulid? EntiteId, LigneAppuiEnAttente Ligne)>> EnAttenteAsync(AppDbContext ctx, Ulid axeId, Ulid exerciceId)
    {
        var lignes = await ctx.Engagements.AsNoTracking()
            .Where(e => e.ExerciceId == exerciceId && e.Statut == StatutSaisie.Valide
                     && e.Dotation.LigneBudgetaire.SousRubrique.Rubrique.AxeId == axeId)
            .Select(e => new
            {
                e.Id, e.Numero, e.Objet, e.DateEngagement, e.DateValidation, e.Montant,
                EntiteId = e.TiersId ?? e.Dotation.TiersId ?? (e.Dotation.Projet != null ? e.Dotation.Projet.TiersBeneficiaireId : null),
                Projet = e.Dotation.Projet != null ? e.Dotation.Projet.Code : null,
                LigneCode = e.Dotation.LigneBudgetaire.Code,
                Verse = e.Paiements.Where(p => p.Statut == StatutSaisie.Valide).Sum(p => (decimal?)p.Montant) ?? 0,
                Rejets = e.RejetsPenalites.Sum(r => (decimal?)r.Montant) ?? 0
            })
            .Where(x => x.Montant - x.Verse - x.Rejets > 0)
            .OrderBy(x => x.DateValidation).ThenBy(x => x.Numero)
            .ToListAsync();

        var entitesIds = lignes.Select(l => l.EntiteId).OfType<Ulid>().Distinct().ToList();
        var noms = await ctx.Tiers.AsNoTracking().Where(t => entitesIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.RaisonSociale);
        return lignes.Select(l => (l.EntiteId, new LigneAppuiEnAttente(l.Id, l.Numero, l.Objet,
            l.EntiteId is { } id ? noms.GetValueOrDefault(id) : null, l.Projet, l.LigneCode, l.DateEngagement, l.DateValidation,
            l.Montant, l.Verse, l.Rejets))).ToList();
    }
}
