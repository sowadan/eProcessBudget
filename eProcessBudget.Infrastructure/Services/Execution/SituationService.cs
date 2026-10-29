using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>Critères de la liste des dotations d'un exercice.</summary>
public sealed class FiltreDotations
{
    public Ulid ExerciceId { get; set; }
    public Ulid? AxeId { get; set; }
    public Ulid? RubriqueId { get; set; }
    public Ulid? ProjetId { get; set; }
    public Ulid? SiteId { get; set; }
    public Ulid? StructureId { get; set; }
    public Ulid? TiersId { get; set; }

    /// <summary>Code ou libellé de la ligne.</summary>
    public string? Recherche { get; set; }
}

/// <summary>Une dotation et sa situation (§ 3.4).</summary>
public sealed record LigneDotation(
    Ulid Id, Ulid ExerciceId, Ulid LigneBudgetaireId, string Axe, string Rubrique, string LigneCode, string LigneLibelle,
    Ulid? ProjetId, string? Projet, Ulid? SiteId, string? Site, Ulid? StructureId, string? Structure, Ulid? TiersId, string? Tiers,
    StatutSaisie Statut, MontantsDotation Montants)
{
    /// <summary>Projet, site, service, entité : ce qui distingue la dotation des autres de la même ligne.</summary>
    public string Imputation => string.Join(" · ", new[] { Projet, Site, Structure, Tiers }.Where(x => x is not null));
}

public sealed record LigneAjustement(
    Ulid Id, string Numero, DateOnly Date, Ulid DotationId, string LigneCode, string LigneLibelle, string Imputation,
    decimal Montant, string Motif, string Justification, StatutSaisie Statut, DateTime? DateValidation);

public sealed record LigneEngagement(
    Ulid Id, string Numero, TypeEngagement Type, DateOnly Date, string Objet, Ulid DotationId, string LigneCode, string LigneLibelle,
    string? Tiers, decimal Montant, decimal Paye, decimal Rejets, StatutSaisie Statut, bool EstReport, bool DepassementEnAttente)
{
    public decimal ResteAPayer => CalculsExecution.ResteAPayer(Montant, Paye, Rejets);
}

public sealed record LigneReport(Ulid Id, TypeReport Type, OrigineReport Origine, int AnneeSource, decimal Montant, string? Engagement, string? Commentaire);

/// <summary>Fiche d'une dotation : situation et pièces qui la composent.</summary>
public sealed record FicheDotation(
    LigneDotation Dotation, int Annee, StatutExercice StatutExercice,
    List<LigneAjustement> Ajustements, List<LigneEngagement> Engagements, List<LigneReport> Reports);

/// <summary>
/// Situation des dotations (cahier fonctionnel § 3.4, technique § 4.4) : prévision
/// initiale, ajustements, reports, AE, paiements, restes et taux sur les deux bases.
/// Écran générique « situation d'une dotation », réutilisé par les quatre axes.
/// </summary>
public interface ISituationService
{
    Task<(List<LigneDotation> Items, int Total)> ListerAsync(FiltreDotations filtre, int page = 1, int taille = 20);

    /// <summary>Totaux de toutes les dotations du filtre (pas seulement de la page).</summary>
    Task<MontantsDotation> TotauxAsync(FiltreDotations filtre);

    Task<FicheDotation?> ObtenirAsync(Ulid dotationId);

    /// <summary>Dotations validées d'un exercice, pour un choix (ajustement, engagement) : 50 au plus.</summary>
    Task<List<ElementListe>> RechercherAsync(Ulid exerciceId, string? recherche);
}

public sealed class SituationService : ISituationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesExecution _acces;

    public SituationService(IDbContextFactory<AppDbContext> factory, AccesExecution acces)
    {
        _factory = factory;
        _acces = acces;
    }

    public async Task<(List<LigneDotation> Items, int Total)> ListerAsync(FiltreDotations filtre, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var source = Filtrer(ctx.Dotations.AsNoTracking(), filtre);
        var total = await source.CountAsync();
        var lignes = await Calculer(ctx, source
                .OrderBy(d => d.LigneBudgetaire.SousRubrique.Rubrique.Axe.Ordre)
                .ThenBy(d => d.LigneBudgetaire.Code).ThenBy(d => d.Id)
                .Skip((page - 1) * taille).Take(taille))
            .ToListAsync();
        return (lignes.Select(l => l.VersLigne()).ToList(), total);
    }

    public async Task<MontantsDotation> TotauxAsync(FiltreDotations filtre)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return MontantsDotation.Zero;

        await using var ctx = await _factory.CreateDbContextAsync();
        var montants = await Calculer(ctx, Filtrer(ctx.Dotations.AsNoTracking(), filtre)).ToListAsync();
        return MontantsDotation.Somme(montants.Select(m => m.Montants));
    }

    public async Task<FicheDotation?> ObtenirAsync(Ulid dotationId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        var calcul = await Calculer(ctx, ctx.Dotations.AsNoTracking().Where(d => d.Id == dotationId)).FirstOrDefaultAsync();
        if (calcul is null) return null;
        var exercice = await ctx.Exercices.AsNoTracking().Where(e => e.Id == calcul.ExerciceId)
                                .Select(e => new { e.Annee, e.Statut }).FirstAsync();

        var ajustements = await ProjeterAjustements(ctx.Ajustements.AsNoTracking().Where(a => a.DotationId == dotationId))
                                .OrderByDescending(a => a.Date).ThenByDescending(a => a.Numero).ToListAsync();
        var engagements = await ProjeterEngagements(ctx, ctx.Engagements.AsNoTracking().Where(e => e.DotationId == dotationId))
                                .OrderByDescending(e => e.Date).ThenByDescending(e => e.Numero).ToListAsync();
        var reports = await ctx.Reports.AsNoTracking().Where(r => r.DotationCibleId == dotationId)
            .OrderBy(r => r.TypeReport)
            .Select(r => new LigneReport(r.Id, r.TypeReport, r.Origine, r.ExerciceSource.Annee, r.Montant,
                                         r.EngagementSource != null ? r.EngagementSource.Numero : null, r.Commentaire))
            .ToListAsync();

        return new FicheDotation(calcul.VersLigne(), exercice.Annee, exercice.Statut, ajustements, engagements, reports);
    }

    public async Task<List<ElementListe>> RechercherAsync(Ulid exerciceId, string? recherche)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Dotations.AsNoTracking().Where(d => d.ExerciceId == exerciceId && d.Statut == StatutSaisie.Valide);
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            q = q.Where(d => d.LigneBudgetaire.Code.Contains(r) || d.LigneBudgetaire.Libelle.Contains(r)
                          || (d.Projet != null && d.Projet.Code.Contains(r)));
        }
        var lignes = await q.OrderBy(d => d.LigneBudgetaire.Code).Take(50)
            .Select(d => new
            {
                d.Id, d.LigneBudgetaire.Code, d.LigneBudgetaire.Libelle,
                Projet = d.Projet != null ? d.Projet.Code : null, Site = d.Site != null ? d.Site.Nom : null,
                Structure = d.Structure != null ? d.Structure.Code : null, Tiers = d.Tiers != null ? d.Tiers.RaisonSociale : null
            })
            .ToListAsync();
        return lignes.Select(l => new ElementListe(l.Id,
            $"{l.Code} — {l.Libelle}" + string.Concat(new[] { l.Projet, l.Site, l.Structure, l.Tiers }.Where(x => x is not null).Select(x => " · " + x))))
            .ToList();
    }

    // ═════ Requêtes partagées par les services du module ════════════════════════

    internal static IQueryable<Dotation> Filtrer(IQueryable<Dotation> q, FiltreDotations f)
    {
        q = q.Where(d => d.ExerciceId == f.ExerciceId);
        if (f.AxeId is { } axe) q = q.Where(d => d.LigneBudgetaire.SousRubrique.Rubrique.AxeId == axe);
        if (f.RubriqueId is { } rubrique) q = q.Where(d => d.LigneBudgetaire.SousRubrique.RubriqueId == rubrique);
        if (f.ProjetId is { } projet) q = q.Where(d => d.ProjetId == projet);
        if (f.SiteId is { } site) q = q.Where(d => d.SiteId == site);
        if (f.StructureId is { } structure) q = q.Where(d => d.StructureId == structure);
        if (f.TiersId is { } tiers) q = q.Where(d => d.TiersId == tiers);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(d => d.LigneBudgetaire.Code.Contains(r) || d.LigneBudgetaire.Libelle.Contains(r));
        }
        return q;
    }

    /// <summary>
    /// Montants de chaque dotation, calculés en base par sous-requêtes (pièces validées
    /// seulement, conventions de bud.vw_SituationDotation).
    /// </summary>
    internal static IQueryable<DotationCalculee> Calculer(AppDbContext ctx, IQueryable<Dotation> source) => Calculer(ctx, source, null);

    /// <summary>
    /// Situation à une date : seules comptent les pièces datées au plus tard ce jour-là
    /// (ajustements, engagements, paiements, rejets) ; les reports, reçus à l'ouverture,
    /// comptent toujours. Sans date, la situation courante.
    /// </summary>
    internal static IQueryable<DotationCalculee> Calculer(AppDbContext ctx, IQueryable<Dotation> source, DateOnly? jusquAu)
        => source.Select(d => new DotationCalculee
        {
            Id = d.Id,
            ExerciceId = d.ExerciceId,
            LigneBudgetaireId = d.LigneBudgetaireId,
            AxeId = d.LigneBudgetaire.SousRubrique.Rubrique.AxeId,
            Axe = d.LigneBudgetaire.SousRubrique.Rubrique.Axe.Libelle,
            RubriqueId = d.LigneBudgetaire.SousRubrique.RubriqueId,
            SousRubriqueId = d.LigneBudgetaire.SousRubriqueId,
            NatureDepenseId = d.LigneBudgetaire.NatureDepenseId,
            SourceFinancementId = d.LigneBudgetaire.SourceFinancementId,
            Rubrique = d.LigneBudgetaire.SousRubrique.Rubrique.Code,
            LigneCode = d.LigneBudgetaire.Code,
            LigneLibelle = d.LigneBudgetaire.Libelle,
            ProjetId = d.ProjetId,
            Projet = d.Projet != null ? d.Projet.Code : null,
            SiteId = d.SiteId,
            Site = d.Site != null ? d.Site.Nom : null,
            StructureId = d.StructureId,
            Structure = d.Structure != null ? d.Structure.Code : null,
            TiersId = d.TiersId,
            Tiers = d.Tiers != null ? d.Tiers.RaisonSociale : null,
            Statut = d.Statut,
            MontantInitial = d.MontantInitial,
            Ajustements = d.Ajustements.Where(a => a.Statut == StatutSaisie.Valide && (jusquAu == null || a.DateAjustement <= jusquAu))
                                .Sum(a => (decimal?)a.Montant) ?? 0,
            Reports = ctx.Reports.Where(r => r.DotationCibleId == d.Id).Sum(r => (decimal?)r.Montant) ?? 0,
            AEPeriode = d.Engagements.Where(e => e.Statut == StatutSaisie.Valide && !e.EstReport && (jusquAu == null || e.DateEngagement <= jusquAu))
                                .Sum(e => (decimal?)e.Montant) ?? 0,
            AEReports = d.Engagements.Where(e => e.Statut == StatutSaisie.Valide && e.EstReport).Sum(e => (decimal?)e.Montant) ?? 0,
            PaiementsPeriode = d.Engagements.Where(e => e.Statut == StatutSaisie.Valide && !e.EstReport)
                                .SelectMany(e => e.Paiements).Where(p => p.Statut == StatutSaisie.Valide && (jusquAu == null || p.DatePaiement <= jusquAu))
                                .Sum(p => (decimal?)p.Montant) ?? 0,
            PaiementsTotal = d.Engagements.Where(e => e.Statut == StatutSaisie.Valide)
                                .SelectMany(e => e.Paiements).Where(p => p.Statut == StatutSaisie.Valide && (jusquAu == null || p.DatePaiement <= jusquAu))
                                .Sum(p => (decimal?)p.Montant) ?? 0,
            Rejets = d.Engagements.Where(e => e.Statut == StatutSaisie.Valide)
                                .SelectMany(e => e.RejetsPenalites).Where(j => jusquAu == null || j.DateConstat <= jusquAu)
                                .Sum(j => (decimal?)j.Montant) ?? 0,
        });

    internal static async Task<MontantsDotation?> MontantsAsync(AppDbContext ctx, Ulid dotationId)
        => (await Calculer(ctx, ctx.Dotations.AsNoTracking().Where(d => d.Id == dotationId)).FirstOrDefaultAsync())?.Montants;

    internal static IQueryable<LigneAjustement> ProjeterAjustements(IQueryable<Ajustement> q)
        => q.Select(a => new LigneAjustement(a.Id, a.Numero, a.DateAjustement, a.DotationId,
            a.Dotation.LigneBudgetaire.Code, a.Dotation.LigneBudgetaire.Libelle,
            (a.Dotation.Projet != null ? a.Dotation.Projet.Code + " " : "") + (a.Dotation.Site != null ? a.Dotation.Site.Nom + " " : "")
                + (a.Dotation.Structure != null ? a.Dotation.Structure.Code : ""),
            a.Montant, a.MotifAjustement.Libelle, a.Justification, a.Statut, a.DateValidation));

    internal static IQueryable<LigneEngagement> ProjeterEngagements(AppDbContext ctx, IQueryable<Engagement> q)
        => q.Select(e => new LigneEngagement(e.Id, e.Numero, e.TypeEngagement, e.DateEngagement, e.Objet, e.DotationId,
            e.Dotation.LigneBudgetaire.Code, e.Dotation.LigneBudgetaire.Libelle,
            e.Tiers != null ? e.Tiers.RaisonSociale : null, e.Montant,
            e.Paiements.Where(p => p.Statut == StatutSaisie.Valide).Sum(p => (decimal?)p.Montant) ?? 0,
            e.RejetsPenalites.Sum(j => (decimal?)j.Montant) ?? 0,
            e.Statut, e.EstReport,
            ctx.ValidationsDepassement.Any(v => v.EngagementId == e.Id && v.Statut == StatutValidation.EnAttente)));
}

/// <summary>Projection d'une dotation et de ses montants (classe à propriétés : traduisible par EF Core).</summary>
internal sealed class DotationCalculee
{
    public Ulid Id { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid AxeId { get; set; }
    public string Axe { get; set; } = "";
    public Ulid RubriqueId { get; set; }
    public Ulid SousRubriqueId { get; set; }
    public Ulid? NatureDepenseId { get; set; }
    public Ulid SourceFinancementId { get; set; }
    public string Rubrique { get; set; } = "";
    public string LigneCode { get; set; } = "";
    public string LigneLibelle { get; set; } = "";
    public Ulid? ProjetId { get; set; }
    public string? Projet { get; set; }
    public Ulid? SiteId { get; set; }
    public string? Site { get; set; }
    public Ulid? StructureId { get; set; }
    public string? Structure { get; set; }
    public Ulid? TiersId { get; set; }
    public string? Tiers { get; set; }
    public StatutSaisie Statut { get; set; }
    public decimal MontantInitial { get; set; }
    public decimal Ajustements { get; set; }
    public decimal Reports { get; set; }
    public decimal AEPeriode { get; set; }
    public decimal AEReports { get; set; }
    public decimal PaiementsPeriode { get; set; }
    public decimal PaiementsTotal { get; set; }
    public decimal Rejets { get; set; }

    public MontantsDotation Montants => new(MontantInitial, Ajustements, Reports, AEPeriode, AEReports, PaiementsPeriode, PaiementsTotal, Rejets);

    public LigneDotation VersLigne() => new(Id, ExerciceId, LigneBudgetaireId, Axe, Rubrique, LigneCode, LigneLibelle,
        ProjetId, Projet, SiteId, Site, StructureId, Structure, TiersId, Tiers, Statut, Montants);
}
