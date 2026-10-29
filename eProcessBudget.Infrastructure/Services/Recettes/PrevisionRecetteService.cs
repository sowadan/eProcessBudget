using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Recettes;

/// <summary>Critères de la liste des prévisions de recettes d'un exercice.</summary>
public sealed class FiltrePrevisions
{
    public Ulid ExerciceId { get; set; }
    public CategorieRecette? Categorie { get; set; }
    public Ulid? NatureRecetteId { get; set; }
    public Ulid? SiteId { get; set; }

    /// <summary>Code ou libellé de la ligne ou de la nature.</summary>
    public string? Recherche { get; set; }
}

/// <summary>Une prévision de recette et sa situation.</summary>
public sealed record LignePrevision(
    Ulid Id, Ulid LigneBudgetaireId, string LigneCode, string LigneLibelle, Ulid NatureRecetteId, string Nature, CategorieRecette Categorie,
    Ulid? SiteId, string? Site, string? Collecteur, string? MotifRevision, int NbEcheances, MontantsRecette Montants)
{
    /// <summary>Nature, site, collecteur : ce qui distingue la prévision des autres de la même ligne.</summary>
    public string Imputation => string.Join(" · ", new[] { Nature, Site, Collecteur }.Where(x => x is not null));
}

public sealed class SaisiePrevision
{
    public Ulid Id { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid? LigneBudgetaireId { get; set; }

    /// <summary>Par défaut, la nature de la ligne ; sinon l'une de ses sous-natures.</summary>
    public Ulid? NatureRecetteId { get; set; }

    public Ulid? SiteId { get; set; }
    public Ulid? TiersCollecteurId { get; set; }
    public decimal MontantInitial { get; set; }
}

/// <summary>Une échéance de recouvrement et sa couverture par les encaissements.</summary>
public sealed record LigneEcheance(Ulid Id, DateOnly Date, decimal Attendu, decimal Couvert, bool EnRetard)
{
    public decimal Reste => Attendu - Couvert;
    public string Statut => LibellesRecettes.StatutEcheance(Attendu, Couvert, EnRetard);
}

/// <summary>
/// Prévisions de recettes (cahier fonctionnel § 3.9.2, technique § 4.6) : par exercice,
/// ligne de recette, nature, site (poste de péage ou de pesage) et organisme collecteur.
///
/// La prévision initiale se saisit tant que l'exercice est en préparation et se fige à son
/// ouverture ; elle n'évolue plus ensuite que par une révision motivée (prévision révisée,
/// qui devient la prévision retenue). Chaque prévision peut porter un échéancier de
/// recouvrement.
/// </summary>
public interface IPrevisionRecetteService
{
    Task<(List<LignePrevision> Items, int Total)> ListerAsync(FiltrePrevisions filtre, int page = 1, int taille = 20);

    /// <summary>Totaux de toutes les prévisions du filtre (pas seulement de la page).</summary>
    Task<MontantsRecette> TotauxAsync(FiltrePrevisions filtre);

    Task<SaisiePrevision?> ObtenirSaisieAsync(Ulid previsionId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisiePrevision saisie);

    /// <summary>Révise la prévision d'un exercice ouvert ; le motif est obligatoire.</summary>
    Task<(bool Ok, string? Erreur)> ReviserAsync(Ulid previsionId, decimal montant, string motif);

    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid previsionId);

    Task<List<LigneEcheance>> ObtenirEcheancierAsync(Ulid previsionId);

    /// <summary>Remplace l'échéancier (Σ montants attendus ≤ prévision retenue).</summary>
    Task<(bool Ok, string? Erreur)> DefinirEcheancierAsync(Ulid previsionId, IReadOnlyList<(DateOnly Date, decimal Montant)> echeances);

    /// <summary>Lignes de recette actives, pour un choix.</summary>
    Task<List<ElementListe>> ListerLignesAsync();

    /// <summary>Nature d'une ligne de recette et ses sous-natures actives.</summary>
    Task<List<ElementListe>> ListerNaturesAsync(Ulid ligneBudgetaireId);

    /// <summary>Prévisions d'un exercice, pour un choix (saisie d'un encaissement) : 200 au plus.</summary>
    Task<List<ElementListe>> RechercherAsync(Ulid exerciceId);
}

public sealed class PrevisionRecetteService : IPrevisionRecetteService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesRecettes _acces;
    private readonly IAuditService _audit;

    public PrevisionRecetteService(IDbContextFactory<AppDbContext> factory, AccesRecettes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    // ═════ Lecture ════════════════════════════════════════════════════════════

    public async Task<(List<LignePrevision> Items, int Total)> ListerAsync(FiltrePrevisions f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = Filtrer(ctx.PrevisionsRecettes.AsNoTracking(), f);
        var total = await q.CountAsync();
        var items = await Projeter(q.OrderBy(p => p.LigneBudgetaire.Code).ThenBy(p => p.NatureRecette.Code).ThenBy(p => p.Site!.Code).ThenBy(p => p.Id)
            .Skip((page - 1) * taille).Take(taille)).ToListAsync();
        return (items.Select(i => i.VersLigne()).ToList(), total);
    }

    public async Task<MontantsRecette> TotauxAsync(FiltrePrevisions f)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return MontantsRecette.Zero;
        await using var ctx = await _factory.CreateDbContextAsync();
        var lignes = await Projeter(Filtrer(ctx.PrevisionsRecettes.AsNoTracking(), f)).ToListAsync();
        return MontantsRecette.Somme(lignes.Select(l => l.Montants));
    }

    public async Task<SaisiePrevision?> ObtenirSaisieAsync(Ulid previsionId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.Id == previsionId)
            .Select(p => new SaisiePrevision
            {
                Id = p.Id, ExerciceId = p.ExerciceId, LigneBudgetaireId = p.LigneBudgetaireId, NatureRecetteId = p.NatureRecetteId,
                SiteId = p.SiteId, TiersCollecteurId = p.TiersCollecteurId, MontantInitial = p.MontantInitial
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<LigneEcheance>> ObtenirEcheancierAsync(Ulid previsionId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var encaisse = await EncaisseAsync(ctx, previsionId);
        var echeances = await ctx.EcheancesRecouvrement.AsNoTracking().Where(e => e.PrevisionRecetteId == previsionId)
            .OrderBy(e => e.DateEcheance).Select(e => new { e.Id, e.DateEcheance, e.MontantAttendu }).ToListAsync();
        var couverture = CalculsRecettes.Couverture(echeances.Select(e => (e.DateEcheance, e.MontantAttendu)), encaisse, Aujourdhui);
        return echeances.Select((e, i) => new LigneEcheance(e.Id, e.DateEcheance, e.MontantAttendu, couverture[i].Couvert, couverture[i].EnRetard)).ToList();
    }

    public async Task<List<ElementListe>> ListerLignesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.LignesBudgetaires.AsNoTracking()
            .Where(l => l.EstActif && l.Sens == SensLigne.Recette)
            .OrderBy(l => l.Code)
            .Select(l => new ElementListe(l.Id, l.Code + " — " + l.Libelle))
            .ToListAsync();
    }

    public async Task<List<ElementListe>> ListerNaturesAsync(Ulid ligneBudgetaireId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var natureLigne = await ctx.LignesBudgetaires.AsNoTracking().Where(l => l.Id == ligneBudgetaireId).Select(l => l.NatureRecetteId).FirstOrDefaultAsync();
        if (natureLigne is null) return [];
        return await ctx.NaturesRecette.AsNoTracking()
            .Where(n => n.EstActif && (n.Id == natureLigne || n.NatureParentId == natureLigne))
            .OrderBy(n => n.NatureParentId != null).ThenBy(n => n.Code)
            .Select(n => new ElementListe(n.Id, n.Code + " — " + n.Libelle))
            .ToListAsync();
    }

    public async Task<List<ElementListe>> RechercherAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var lignes = await ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.ExerciceId == exerciceId)
            .OrderBy(p => p.NatureRecette.Code).ThenBy(p => p.Site!.Code).Take(200)
            .Select(p => new
            {
                p.Id, Ligne = p.LigneBudgetaire.Code, Nature = p.NatureRecette.Libelle,
                Site = p.Site != null ? p.Site.Nom : null, Collecteur = p.TiersCollecteur != null ? p.TiersCollecteur.RaisonSociale : null
            })
            .ToListAsync();
        return lignes.Select(l => new ElementListe(l.Id,
            $"{l.Ligne} — {l.Nature}" + string.Concat(new[] { l.Site, l.Collecteur }.Where(x => x is not null).Select(x => " · " + x)))).ToList();
    }

    // ═════ Saisie ═════════════════════════════════════════════════════════════

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisiePrevision s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PrevisionRecetteSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("saisir les prévisions de recettes"));

        if (s.LigneBudgetaireId is not { } ligneId) return (false, "Choisissez la ligne de recette.");
        if (s.MontantInitial < 0 || !CalculsExecution.EstMontantValide(s.MontantInitial))
            return (false, "La prévision initiale est un montant positif ou nul, à 2 décimales au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Id == s.ExerciceId);
        if (exercice is null) return (false, "Exercice introuvable.");
        if (exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé : plus aucune saisie.");
        var enPreparation = exercice.Statut == StatutExercice.EnPreparation;

        var ligne = await ctx.LignesBudgetaires.AsNoTracking().Where(l => l.Id == ligneId)
            .Select(l => new { l.EstActif, l.Sens, l.NatureRecetteId, l.SousRubrique.Rubrique.AxeId }).FirstOrDefaultAsync();
        if (ligne is null || !ligne.EstActif) return (false, "Ligne budgétaire introuvable ou inactive.");
        if (ligne.Sens != SensLigne.Recette) return (false, "Une prévision de recette porte sur une ligne de recette.");

        // Nature : celle de la ligne, ou l'une de ses sous-natures (par poste, par taxe...).
        var natureId = s.NatureRecetteId ?? ligne.NatureRecetteId;
        var nature = natureId is null ? null : await ctx.NaturesRecette.AsNoTracking().Where(n => n.Id == natureId)
            .Select(n => new { n.EstActif, n.NatureParentId, n.SuiviParSite, n.CategorieRecette }).FirstOrDefaultAsync();
        if (nature is null || !nature.EstActif) return (false, "Nature de recette introuvable ou inactive.");
        if (natureId != ligne.NatureRecetteId && nature.NatureParentId != ligne.NatureRecetteId)
            return (false, "La nature doit être celle de la ligne ou l'une de ses sous-natures.");
        if (nature.CategorieRecette == CategorieRecette.ReportRessourcesAnterieures)
            return (false, "Les reports de ressources antérieures se saisissent dans l'écran Reports de ressources, pas en prévision.");
        if (nature.SuiviParSite && s.SiteId is null) return (false, "Cette nature se suit par site : indiquez le poste concerné.");

        if (s.SiteId is { } site && !await ctx.Sites.AnyAsync(x => x.Id == site && x.EstActif)) return (false, "Site introuvable ou inactif.");
        if (s.TiersCollecteurId is { } collecteur
            && !await ctx.Tiers.AnyAsync(x => x.Id == collecteur && x.EstActif && x.TypeTiers == TypeTiers.OrganismeCollecteur))
            return (false, "Le collecteur doit être un tiers actif de type « Organisme collecteur ».");

        var refus = await _acces.PerimetreAsync(acteurId, ligne.AxeId, s.SiteId);
        if (refus is not null) return (false, refus);

        // Une seule prévision par ligne, nature, site et collecteur sur l'exercice.
        if (await ctx.PrevisionsRecettes.AnyAsync(p => p.Id != s.Id && p.ExerciceId == s.ExerciceId && p.LigneBudgetaireId == ligneId
                && p.NatureRecetteId == natureId && p.SiteId == s.SiteId && p.TiersCollecteurId == s.TiersCollecteurId))
            return (false, "Une prévision existe déjà pour cette ligne, cette nature, ce site et ce collecteur sur l'exercice.");

        PrevisionRecette prevision;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            if (!enPreparation && s.MontantInitial != 0)
                return (false, "L'exercice est ouvert : la prévision initiale est figée. Créez la prévision à 0 et révisez-la.");
            prevision = new PrevisionRecette { ExerciceId = s.ExerciceId, CreePar = acteurId };
            ctx.PrevisionsRecettes.Add(prevision);
        }
        else
        {
            var existante = await ctx.PrevisionsRecettes.FirstOrDefaultAsync(p => p.Id == s.Id);
            if (existante is null) return (false, "Prévision introuvable.");
            if (existante.ExerciceId != s.ExerciceId) return (false, "Une prévision ne change pas d'exercice.");
            if (!enPreparation) return (false, "La prévision initiale est figée depuis l'ouverture de l'exercice : révisez-la.");
            // Changer de ligne ou de site exige aussi le périmètre de ceux qu'elle quitte.
            if ((existante.LigneBudgetaireId != ligneId || existante.SiteId != s.SiteId)
                && await _acces.PerimetrePrevisionAsync(ctx, acteurId, existante.Id) is { } refusOrigine)
                return (false, refusOrigine);
            prevision = existante;
            avant = new { prevision.LigneBudgetaireId, prevision.NatureRecetteId, prevision.SiteId, prevision.MontantInitial };
        }

        prevision.LigneBudgetaireId = ligneId;
        prevision.NatureRecetteId = natureId!.Value;
        prevision.SiteId = s.SiteId;
        prevision.TiersCollecteurId = s.TiersCollecteurId;
        prevision.MontantInitial = s.MontantInitial;
        prevision.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesRecettes.ModuleAudit,
            nameof(PrevisionRecette), prevision.Id.ToString(), avant,
            new { prevision.LigneBudgetaireId, prevision.NatureRecetteId, prevision.SiteId, prevision.MontantInitial });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ReviserAsync(Ulid previsionId, decimal montant, string motif)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PrevisionRecetteSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("réviser une prévision de recette"));
        motif = motif?.Trim() ?? "";
        if (motif.Length == 0) return (false, "Le motif de la révision est obligatoire.");
        if (motif.Length > 500) return (false, "Motif : 500 caractères au plus.");
        if (montant < 0 || !CalculsExecution.EstMontantValide(montant))
            return (false, "La prévision révisée est un montant positif ou nul, à 2 décimales au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var prevision = await ctx.PrevisionsRecettes.Include(p => p.Exercice).FirstOrDefaultAsync(p => p.Id == previsionId);
        if (prevision is null) return (false, "Prévision introuvable.");
        if (prevision.Exercice.Statut != StatutExercice.Ouvert)
            return (false, "La révision porte sur un exercice ouvert ; en préparation, modifiez la prévision initiale.");
        var refus = await _acces.PerimetrePrevisionAsync(ctx, acteurId, previsionId);
        if (refus is not null) return (false, refus);

        var echeancier = await ctx.EcheancesRecouvrement.Where(e => e.PrevisionRecetteId == previsionId).SumAsync(e => (decimal?)e.MontantAttendu) ?? 0;
        if (montant < echeancier)
            return (false, $"L'échéancier de recouvrement attend {echeancier:N2} : la prévision ne peut pas descendre en dessous.");

        var avant = new { prevision.MontantRevise, prevision.MotifRevision };
        prevision.MontantRevise = montant;
        prevision.MotifRevision = motif;
        prevision.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "MODIFICATION", AccesRecettes.ModuleAudit, nameof(PrevisionRecette),
            prevision.Id.ToString(), avant, new { prevision.MontantRevise, prevision.MotifRevision }, commentaire: "Révision");
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid previsionId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PrevisionRecetteSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("supprimer une prévision de recette"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var prevision = await ctx.PrevisionsRecettes.Include(p => p.Exercice).FirstOrDefaultAsync(p => p.Id == previsionId);
        if (prevision is null) return (false, "Prévision introuvable.");
        if (prevision.Exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé.");
        var refus = await _acces.PerimetrePrevisionAsync(ctx, acteurId, previsionId);
        if (refus is not null) return (false, refus);

        if (await ctx.Encaissements.AnyAsync(e => e.PrevisionRecetteId == previsionId)
            || await ctx.EmissionsRecettes.AnyAsync(e => e.PrevisionRecetteId == previsionId))
            return (false, "Cette prévision porte des factures émises ou des encaissements : elle ne peut pas être supprimée.");
        if (prevision.Exercice.Statut == StatutExercice.Ouvert && (prevision.MontantInitial != 0 || prevision.MontantRevise is not null))
            return (false, "La prévision de cet exercice ouvert est figée : elle ne peut plus être supprimée (révisez-la à 0 si besoin).");

        foreach (var e in await ctx.EcheancesRecouvrement.Where(e => e.PrevisionRecetteId == previsionId).ToListAsync())
        {
            e.EstSupprime = true;
            e.SupprimePar = acteurId;
        }
        prevision.EstSupprime = true;
        prevision.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesRecettes.ModuleAudit, nameof(PrevisionRecette),
            prevision.Id.ToString(), new { prevision.LigneBudgetaireId, prevision.NatureRecetteId, prevision.MontantInitial }, null);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> DefinirEcheancierAsync(Ulid previsionId, IReadOnlyList<(DateOnly Date, decimal Montant)> echeances)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PrevisionRecetteSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("définir l'échéancier de recouvrement"));
        if (echeances.Any(e => e.Montant <= 0 || !CalculsExecution.EstMontantValide(e.Montant)))
            return (false, "Chaque montant attendu est positif, à 2 décimales au plus.");
        if (echeances.GroupBy(e => e.Date).Any(g => g.Count() > 1)) return (false, "Deux échéances tombent le même jour : regroupez-les.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var prevision = await ctx.PrevisionsRecettes.Include(p => p.Exercice).FirstOrDefaultAsync(p => p.Id == previsionId);
        if (prevision is null) return (false, "Prévision introuvable.");
        if (prevision.Exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé.");
        if (echeances.Any(e => e.Date < prevision.Exercice.DateDebut || e.Date > prevision.Exercice.DateFin))
            return (false, $"Les échéances tombent dans l'exercice {prevision.Exercice.Annee}.");
        var retenue = CalculsRecettes.PrevisionRetenue(prevision.MontantInitial, prevision.MontantRevise);
        if (echeances.Sum(e => e.Montant) > retenue)
            return (false, $"L'échéancier ({echeances.Sum(e => e.Montant):N2}) dépasse la prévision retenue ({retenue:N2}).");
        var refus = await _acces.PerimetrePrevisionAsync(ctx, acteurId, previsionId);
        if (refus is not null) return (false, refus);

        var anciennes = await ctx.EcheancesRecouvrement.Where(e => e.PrevisionRecetteId == previsionId).ToListAsync();
        foreach (var e in anciennes)
        {
            e.EstSupprime = true;
            e.SupprimePar = acteurId;
        }
        foreach (var (date, montant) in echeances)
            ctx.EcheancesRecouvrement.Add(new EcheanceRecouvrement { PrevisionRecetteId = previsionId, DateEcheance = date, MontantAttendu = montant, CreePar = acteurId });
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "MODIFICATION", AccesRecettes.ModuleAudit, nameof(EcheanceRecouvrement), previsionId.ToString(),
            anciennes.Select(e => new { e.DateEcheance, e.MontantAttendu }), echeances.Select(e => new { e.Date, e.Montant }));
        return (true, null);
    }

    // ═════ Requêtes partagées par les services du module ════════════════════════

    internal static DateOnly Aujourdhui => DateOnly.FromDateTime(DateTime.Today);

    internal static IQueryable<PrevisionRecette> Filtrer(IQueryable<PrevisionRecette> q, FiltrePrevisions f)
    {
        q = q.Where(p => p.ExerciceId == f.ExerciceId);
        if (f.Categorie is { } categorie) q = q.Where(p => p.NatureRecette.CategorieRecette == categorie);
        if (f.NatureRecetteId is { } nature) q = q.Where(p => p.NatureRecetteId == nature || p.NatureRecette.NatureParentId == nature);
        if (f.SiteId is { } site) q = q.Where(p => p.SiteId == site);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(p => p.LigneBudgetaire.Code.Contains(r) || p.LigneBudgetaire.Libelle.Contains(r)
                          || p.NatureRecette.Code.Contains(r) || p.NatureRecette.Libelle.Contains(r));
        }
        return q;
    }

    /// <summary>
    /// Prévisions et leurs montants, calculés en base (encaissements validés seulement,
    /// conventions de rec.vw_SituationRecette).
    /// </summary>
    internal static IQueryable<PrevisionCalculee> Projeter(IQueryable<PrevisionRecette> q)
        => q.Select(p => new PrevisionCalculee
        {
            Id = p.Id,
            ExerciceId = p.ExerciceId,
            LigneBudgetaireId = p.LigneBudgetaireId,
            AxeId = p.LigneBudgetaire.SousRubrique.Rubrique.AxeId,
            RubriqueId = p.LigneBudgetaire.SousRubrique.RubriqueId,
            LigneCode = p.LigneBudgetaire.Code,
            LigneLibelle = p.LigneBudgetaire.Libelle,
            NatureRecetteId = p.NatureRecetteId,
            NatureParentId = p.NatureRecette.NatureParentId,
            Nature = p.NatureRecette.Code + " — " + p.NatureRecette.Libelle,
            Categorie = p.NatureRecette.CategorieRecette,
            SiteId = p.SiteId,
            Site = p.Site != null ? p.Site.Nom : null,
            TiersCollecteurId = p.TiersCollecteurId,
            Collecteur = p.TiersCollecteur != null ? p.TiersCollecteur.RaisonSociale : null,
            MotifRevision = p.MotifRevision,
            NbEcheances = p.Echeances.Count,
            MontantInitial = p.MontantInitial,
            MontantRevise = p.MontantRevise,
            Encaisse = p.Encaissements.Where(e => e.Statut == StatutSaisie.Valide).Sum(e => (decimal?)e.Montant) ?? 0,
            EncaissePositionne = p.Encaissements.Where(e => e.Statut == StatutSaisie.Valide && e.EstPositionne).Sum(e => (decimal?)e.Montant) ?? 0,
            Emis = p.Emissions.Where(e => e.Statut == StatutSaisie.Valide).Sum(e => (decimal?)e.Montant) ?? 0,
        });

    internal static async Task<decimal> EncaisseAsync(AppDbContext ctx, Ulid previsionId)
        => await ctx.Encaissements.Where(e => e.PrevisionRecetteId == previsionId && e.Statut == StatutSaisie.Valide)
                                  .SumAsync(e => (decimal?)e.Montant) ?? 0;
}

/// <summary>Projection d'une prévision et de ses montants (classe à propriétés : traduisible par EF Core).</summary>
internal sealed class PrevisionCalculee
{
    public Ulid Id { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid AxeId { get; set; }
    public Ulid RubriqueId { get; set; }
    public string LigneCode { get; set; } = "";
    public string LigneLibelle { get; set; } = "";
    public Ulid NatureRecetteId { get; set; }
    public Ulid? NatureParentId { get; set; }
    public string Nature { get; set; } = "";
    public CategorieRecette Categorie { get; set; }
    public Ulid? SiteId { get; set; }
    public string? Site { get; set; }
    public Ulid? TiersCollecteurId { get; set; }
    public string? Collecteur { get; set; }
    public string? MotifRevision { get; set; }
    public int NbEcheances { get; set; }
    public decimal MontantInitial { get; set; }
    public decimal? MontantRevise { get; set; }
    public decimal Encaisse { get; set; }
    public decimal EncaissePositionne { get; set; }
    public decimal Emis { get; set; }

    public MontantsRecette Montants => new(MontantInitial, CalculsRecettes.PrevisionRetenue(MontantInitial, MontantRevise), Encaisse, EncaissePositionne, Emis);

    public LignePrevision VersLigne() => new(Id, LigneBudgetaireId, LigneCode, LigneLibelle, NatureRecetteId, Nature, Categorie,
        SiteId, Site, Collecteur, MotifRevision, NbEcheances, Montants);
}
