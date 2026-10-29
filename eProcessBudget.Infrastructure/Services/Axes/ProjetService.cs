using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>Critères de la liste des projets d'un axe.</summary>
public sealed class FiltreProjets
{
    /// <summary>Exercice dont on affiche la situation budgétaire des projets.</summary>
    public Ulid? ExerciceId { get; set; }

    public StatutProjet? Statut { get; set; }
    public Ulid? SiteId { get; set; }
    public Ulid? StructureId { get; set; }

    /// <summary>Entité bénéficiaire (appuis).</summary>
    public Ulid? TiersId { get; set; }

    public Ulid? SourceFinancementId { get; set; }
    public Ulid? TypeTravauxId { get; set; }

    /// <summary>Code ou intitulé.</summary>
    public string? Recherche { get; set; }
}

/// <summary>Un projet et sa situation sur l'exercice choisi.</summary>
public sealed record LigneProjet(
    Ulid Id, string Code, string Intitule, StatutProjet Statut, bool EstPluriannuel, DateOnly? DateDebut, DateOnly? DateFin,
    string? Site, string? Structure, string? Beneficiaire, string? Source, string? TypeTravaux, string? TypeVoie,
    decimal? KmPrevus, decimal KmRealises, int NbContrats, MontantsDotation Montants)
{
    /// <summary>Taux d'avancement kilométrique (§ 4.1).</summary>
    public decimal? TauxKm => CalculsAxes.TauxAvancementKm(KmRealises, KmPrevus);
}

/// <summary>
/// Saisie d'un projet ; les champs « travaux » servent aux opérations de l'axe Entretien
/// routier et aux projets de réhabilitation de l'axe Investissements (C.2.4).
/// </summary>
public sealed class SaisieProjet
{
    public Ulid Id { get; set; }
    public string CodeAxe { get; set; } = "";
    public string Code { get; set; } = "";
    public string Intitule { get; set; } = "";
    public string? Description { get; set; }
    public Ulid? SiteId { get; set; }
    public Ulid? StructureId { get; set; }
    public Ulid? TiersBeneficiaireId { get; set; }
    public Ulid? SourceFinancementId { get; set; }
    public DateOnly? DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
    public bool EstPluriannuel { get; set; }
    public StatutProjet Statut { get; set; } = StatutProjet.Programme;

    // Opération de travaux (bud.ProjetTravaux) : entretien, ou réhabilitation en investissement
    public Ulid? TypeVoieId { get; set; }
    public Ulid? TypeTravauxId { get; set; }
    public Ulid? MaitreOuvrageId { get; set; }
    public string? Localisation { get; set; }
    public decimal? KmPrevus { get; set; }
}

/// <summary>En-tête d'un projet, tous axes confondus.</summary>
public sealed record FicheProjet(
    Ulid Id, AxeDepense Axe, string Code, string Intitule, string? Description, StatutProjet Statut,
    bool EstPluriannuel, DateOnly? DateDebut, DateOnly? DateFin,
    string? Site, string? Structure, string? Beneficiaire, string? BeneficiaireParent, string? Source,
    string? TypeVoie, string? TypeTravaux, string? MaitreOuvrage, string? Localisation, decimal? KmPrevus, decimal KmRealises)
{
    public decimal? TauxKm => CalculsAxes.TauxAvancementKm(KmRealises, KmPrevus);
}

/// <summary>Exécution budgétaire d'un projet sur un exercice : dotations, engagements, paiements.</summary>
public sealed record ExecutionProjet(MontantsDotation Totaux, List<LigneDotation> Dotations, List<LigneEngagement> Engagements, List<LignePaiement> Paiements);

/// <summary>
/// Projets et opérations des axes Entretien routier, Investissements et Appuis (cahier
/// fonctionnel § 3.5.2, 3.6.2, 3.8.2 ; technique § 4.5).
///
/// Un projet appartient à un axe ; ses dotations (une par ligne et imputation) portent son
/// budget et suivent le cycle commun. Spécificités : l'opération de travaux (type de voie,
/// type de travaux, maître d'ouvrage, kilométrage prévu), la source de financement RI / RE
/// d'un investissement, l'entité bénéficiaire d'un appui.
/// </summary>
public interface IProjetService
{
    Task<(List<LigneProjet> Items, int Total)> ListerAsync(AxeDepense axe, FiltreProjets filtre, int page = 1, int taille = 20);
    Task<FicheProjet?> ObtenirAsync(Ulid projetId);
    Task<ExecutionProjet?> ObtenirExecutionAsync(Ulid projetId, Ulid exerciceId);
    Task<SaisieProjet?> ObtenirSaisieAsync(Ulid projetId);
    Task<(bool Ok, string? Erreur, Ulid Id)> EnregistrerAsync(SaisieProjet saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid projetId);

    /// <summary>Projets non annulés de l'axe, pour un choix.</summary>
    Task<List<ElementListe>> ListerPourSelectionAsync(AxeDepense axe);

    /// <summary>Opérations de travaux non annulées (entretien, réhabilitation) que l'utilisateur consulte, pour l'avancement physique.</summary>
    Task<List<ElementListe>> ListerOperationsTravauxAsync();
}

public sealed class ProjetService : IProjetService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesAxes _acces;
    private readonly IAuditService _audit;

    public ProjetService(IDbContextFactory<AppDbContext> factory, AccesAxes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    // ═════ Lecture ════════════════════════════════════════════════════════════

    public async Task<(List<LigneProjet> Items, int Total)> ListerAsync(AxeDepense axe, FiltreProjets f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Projets.AsNoTracking().Where(p => p.Axe.Code == axe.Code);
        if (f.Statut is { } statut) q = q.Where(p => p.Statut == statut);
        if (f.SiteId is { } site) q = q.Where(p => p.SiteId == site);
        if (f.StructureId is { } structure) q = q.Where(p => p.StructureId == structure);
        if (f.TiersId is { } tiers) q = q.Where(p => p.TiersBeneficiaireId == tiers || (p.TiersBeneficiaire != null && p.TiersBeneficiaire.TiersParentId == tiers));
        if (f.SourceFinancementId is { } source) q = q.Where(p => p.SourceFinancementId == source);
        if (f.TypeTravauxId is { } typeTravaux) q = q.Where(p => p.Travaux != null && p.Travaux.TypeTravauxId == typeTravaux);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(p => p.Code.Contains(r) || p.Intitule.Contains(r));
        }

        var total = await q.CountAsync();
        var lignes = await q.OrderBy(p => p.Code).Skip((page - 1) * taille).Take(taille)
            .Select(p => new
            {
                p.Id, p.Code, p.Intitule, p.Statut, p.EstPluriannuel, p.DateDebut, p.DateFin,
                Site = p.Site != null ? p.Site.Nom : null,
                Structure = p.Structure != null ? p.Structure.Code : null,
                Beneficiaire = p.TiersBeneficiaire != null ? p.TiersBeneficiaire.RaisonSociale : null,
                Source = p.SourceFinancement != null ? p.SourceFinancement.Code : null,
                TypeTravaux = p.Travaux != null && p.Travaux.TypeTravaux != null ? p.Travaux.TypeTravaux.Libelle : null,
                TypeVoie = p.Travaux != null && p.Travaux.TypeVoie != null ? p.Travaux.TypeVoie.Libelle : null,
                KmPrevus = p.Travaux != null ? p.Travaux.KmPrevus : null,
                NbContrats = p.Contrats.Count
            })
            .ToListAsync();

        var ids = lignes.Select(l => l.Id).ToList();
        var montants = f.ExerciceId is { } exerciceId ? await RequetesAxes.MontantsParProjetAsync(ctx, exerciceId, ids) : [];
        var km = axe != AxesDepense.Appuis ? await RequetesAxes.KmRealisesAsync(ctx, ids) : [];

        return (lignes.Select(l => new LigneProjet(l.Id, l.Code, l.Intitule, l.Statut, l.EstPluriannuel, l.DateDebut, l.DateFin,
            l.Site, l.Structure, l.Beneficiaire, l.Source, l.TypeTravaux, l.TypeVoie, l.KmPrevus, km.GetValueOrDefault(l.Id),
            l.NbContrats, montants.GetValueOrDefault(l.Id) ?? MontantsDotation.Zero)).ToList(), total);
    }

    public async Task<FicheProjet?> ObtenirAsync(Ulid projetId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var p = await ctx.Projets.AsNoTracking().Where(x => x.Id == projetId)
            .Select(x => new
            {
                x.Id, CodeAxe = x.Axe.Code, x.Code, x.Intitule, x.Description, x.Statut, x.EstPluriannuel, x.DateDebut, x.DateFin,
                Site = x.Site != null ? x.Site.Code + " — " + x.Site.Nom : null,
                Structure = x.Structure != null ? x.Structure.Code + " — " + x.Structure.Libelle : null,
                Beneficiaire = x.TiersBeneficiaire != null ? x.TiersBeneficiaire.RaisonSociale : null,
                BeneficiaireParent = x.TiersBeneficiaire != null && x.TiersBeneficiaire.TiersParent != null ? x.TiersBeneficiaire.TiersParent.RaisonSociale : null,
                Source = x.SourceFinancement != null ? x.SourceFinancement.Code + " — " + x.SourceFinancement.Libelle : null,
                TypeVoie = x.Travaux != null && x.Travaux.TypeVoie != null ? x.Travaux.TypeVoie.Libelle : null,
                TypeTravaux = x.Travaux != null && x.Travaux.TypeTravaux != null ? x.Travaux.TypeTravaux.Libelle : null,
                MaitreOuvrage = x.Travaux != null && x.Travaux.MaitreOuvrage != null ? x.Travaux.MaitreOuvrage.RaisonSociale : null,
                Localisation = x.Travaux != null ? x.Travaux.Localisation : null,
                KmPrevus = x.Travaux != null ? x.Travaux.KmPrevus : null
            })
            .FirstOrDefaultAsync();
        if (p is null || AxesDepense.DeCode(p.CodeAxe) is not { } axe) return null;
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return null;

        var km = (await RequetesAxes.KmRealisesAsync(ctx, [p.Id])).GetValueOrDefault(p.Id);
        return new FicheProjet(p.Id, axe, p.Code, p.Intitule, p.Description, p.Statut, p.EstPluriannuel, p.DateDebut, p.DateFin,
            p.Site, p.Structure, p.Beneficiaire, p.BeneficiaireParent, p.Source, p.TypeVoie, p.TypeTravaux, p.MaitreOuvrage,
            p.Localisation, p.KmPrevus, km);
    }

    public async Task<ExecutionProjet?> ObtenirExecutionAsync(Ulid projetId, Ulid exerciceId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await LecteurDuProjetAsync(ctx, projetId) == Ulid.Empty) return null;

        var dotations = await SituationService.Calculer(ctx, ctx.Dotations.AsNoTracking()
                .Where(d => d.ProjetId == projetId && d.ExerciceId == exerciceId)
                .OrderBy(d => d.LigneBudgetaire.Code).ThenBy(d => d.Id))
            .ToListAsync();
        var engagements = await SituationService.ProjeterEngagements(ctx, ctx.Engagements.AsNoTracking()
                .Where(e => e.ExerciceId == exerciceId && e.Dotation.ProjetId == projetId)
                .OrderByDescending(e => e.DateEngagement).ThenByDescending(e => e.Numero))
            .ToListAsync();
        var paiements = await PaiementService.ProjeterPaiements(ctx.Paiements.AsNoTracking()
                .Where(p => p.ExerciceId == exerciceId && p.Engagement.Dotation.ProjetId == projetId)
                .OrderByDescending(p => p.DatePaiement).ThenByDescending(p => p.NumeroPiece))
            .ToListAsync();

        return new ExecutionProjet(MontantsDotation.Somme(dotations.Select(d => d.Montants)),
            dotations.Select(d => d.VersLigne()).ToList(), engagements, paiements);
    }

    public async Task<SaisieProjet?> ObtenirSaisieAsync(Ulid projetId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await LecteurDuProjetAsync(ctx, projetId) == Ulid.Empty) return null;
        return await ctx.Projets.AsNoTracking().Where(p => p.Id == projetId)
            .Select(p => new SaisieProjet
            {
                Id = p.Id, CodeAxe = p.Axe.Code, Code = p.Code, Intitule = p.Intitule, Description = p.Description,
                SiteId = p.SiteId, StructureId = p.StructureId, TiersBeneficiaireId = p.TiersBeneficiaireId,
                SourceFinancementId = p.SourceFinancementId, DateDebut = p.DateDebut, DateFin = p.DateFin,
                EstPluriannuel = p.EstPluriannuel, Statut = p.Statut,
                TypeVoieId = p.Travaux != null ? p.Travaux.TypeVoieId : null,
                TypeTravauxId = p.Travaux != null ? p.Travaux.TypeTravauxId : null,
                MaitreOuvrageId = p.Travaux != null ? p.Travaux.MaitreOuvrageId : null,
                Localisation = p.Travaux != null ? p.Travaux.Localisation : null,
                KmPrevus = p.Travaux != null ? p.Travaux.KmPrevus : null
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<ElementListe>> ListerPourSelectionAsync(AxeDepense axe)
    {
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Projets.AsNoTracking()
            .Where(p => p.Axe.Code == axe.Code && p.Statut != StatutProjet.Annule)
            .OrderBy(p => p.Code)
            .Select(p => new ElementListe(p.Id, p.Code + " — " + p.Intitule))
            .ToListAsync();
    }

    public async Task<List<ElementListe>> ListerOperationsTravauxAsync()
    {
        var axes = new List<string>();
        foreach (var axe in new[] { AxesDepense.Entretien, AxesDepense.Investissement })
            if (await _acces.LecteurAsync(axe) != Ulid.Empty) axes.Add(axe.Code);
        if (axes.Count == 0) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Projets.AsNoTracking()
            .Where(p => p.Travaux != null && p.Statut != StatutProjet.Annule && axes.Contains(p.Axe.Code))
            .OrderBy(p => p.Code)
            .Select(p => new ElementListe(p.Id, p.Code + " — " + p.Intitule + (p.Axe.Code == "INVESTISSEMENT" ? " (réhabilitation)" : "")))
            .ToListAsync();
    }

    // ═════ Saisie ═════════════════════════════════════════════════════════════

    public async Task<(bool Ok, string? Erreur, Ulid Id)> EnregistrerAsync(SaisieProjet s)
    {
        if (AxesDepense.DeCode(s.CodeAxe) is not { } axe || axe == AxesDepense.Fonctionnement)
            return (false, "Les projets relèvent des axes Entretien routier, Investissements et Appuis.", Ulid.Empty);
        var acteurId = await _acces.SaisisseurAsync(axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(axe), Ulid.Empty);

        var code = s.Code?.Trim() ?? "";
        var intitule = s.Intitule?.Trim() ?? "";
        var description = string.IsNullOrWhiteSpace(s.Description) ? null : s.Description.Trim();
        var localisation = string.IsNullOrWhiteSpace(s.Localisation) ? null : s.Localisation.Trim();
        if (code.Length == 0) return (false, "Le code est obligatoire.", Ulid.Empty);
        if (code.Length > 30) return (false, "Code : 30 caractères au plus.", Ulid.Empty);
        if (intitule.Length == 0) return (false, "L'intitulé est obligatoire.", Ulid.Empty);
        if (intitule.Length > 300) return (false, "Intitulé : 300 caractères au plus.", Ulid.Empty);
        if (description?.Length > 2000) return (false, "Description : 2 000 caractères au plus.", Ulid.Empty);
        if (localisation?.Length > 300) return (false, "Localisation : 300 caractères au plus.", Ulid.Empty);
        if (s.DateDebut is { } debut && s.DateFin is { } fin && fin < debut) return (false, "La date de fin précède la date de début.", Ulid.Empty);
        if (!Enum.IsDefined(s.Statut)) return (false, "Statut inconnu.", Ulid.Empty);
        if (s.KmPrevus is { } kmPrevus && (kmPrevus < 0 || decimal.Round(kmPrevus, 4) != kmPrevus))
            return (false, "Le kilométrage prévu est positif, à 4 décimales au plus.", Ulid.Empty);

        await using var ctx = await _factory.CreateDbContextAsync();
        if (await AccesAxes.AxeIdAsync(ctx, axe) is not { } axeId) return (false, AccesAxes.MessageAxeAbsent(axe), Ulid.Empty);

        var erreur = await ControlerReferencesAsync(ctx, axe, s);
        if (erreur is not null) return (false, erreur, Ulid.Empty);

        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: axeId, StructureId: s.StructureId, SiteId: s.SiteId));
        if (refus is not null) return (false, refus, Ulid.Empty);

        if (await ctx.Projets.AnyAsync(p => p.Id != s.Id && p.Code == code))
            return (false, $"Le code {code} est déjà utilisé par un autre projet.", Ulid.Empty);

        Projet projet;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            projet = new Projet { AxeId = axeId, CreePar = acteurId };
            ctx.Projets.Add(projet);
        }
        else
        {
            var existant = await ctx.Projets.Include(p => p.Travaux).FirstOrDefaultAsync(p => p.Id == s.Id);
            if (existant is null) return (false, "Projet introuvable.", Ulid.Empty);
            if (existant.AxeId != axeId) return (false, "Un projet ne change pas d'axe.", Ulid.Empty);
            // Déplacer un projet exige aussi le périmètre du site ou du service qu'il quitte.
            if ((existant.SiteId != s.SiteId || existant.StructureId != s.StructureId)
                && await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: axeId, StructureId: existant.StructureId, SiteId: existant.SiteId)) is { } refusOrigine)
                return (false, refusOrigine, Ulid.Empty);
            if (s.Statut == StatutProjet.Annule && existant.Statut != StatutProjet.Annule
                && await ctx.Engagements.AnyAsync(e => e.Dotation.ProjetId == existant.Id
                                                    && e.Statut != StatutSaisie.Annule && e.Statut != StatutSaisie.Rejete))
                return (false, "Ce projet porte des engagements : annulez-les avant d'annuler le projet.", Ulid.Empty);
            projet = existant;
            avant = new { projet.Code, projet.Intitule, projet.Statut, projet.SiteId, projet.StructureId };
        }

        projet.Code = code;
        projet.Intitule = intitule;
        projet.Description = description;
        projet.SiteId = s.SiteId;
        projet.StructureId = s.StructureId;
        projet.TiersBeneficiaireId = axe == AxesDepense.Appuis ? s.TiersBeneficiaireId : null;
        projet.SourceFinancementId = s.SourceFinancementId;
        projet.DateDebut = s.DateDebut;
        projet.DateFin = s.DateFin;
        projet.EstPluriannuel = s.EstPluriannuel;
        projet.Statut = s.Statut;
        projet.ModifiePar = acteurId;

        if (EstOperationTravaux(axe, s))
        {
            // Complément 1-1 de l'opération de travaux : même identifiant que le projet.
            projet.Travaux ??= new ProjetTravaux { CreePar = acteurId };
            projet.Travaux.TypeVoieId = s.TypeVoieId;
            projet.Travaux.TypeTravauxId = s.TypeTravauxId;
            projet.Travaux.MaitreOuvrageId = s.MaitreOuvrageId;
            projet.Travaux.Localisation = localisation;
            projet.Travaux.KmPrevus = s.KmPrevus;
            projet.Travaux.ModifiePar = acteurId;
        }
        else if (projet.Travaux is { } ancien)
        {
            // Un projet d'investissement qui n'est plus une réhabilitation perd son complément travaux.
            if (await ctx.AvancementsPhysiques.AnyAsync(a => a.ProjetId == projet.Id))
                return (false, "Ce projet porte un avancement physique : il reste un projet de réhabilitation.", Ulid.Empty);
            ancien.EstSupprime = true;
            ancien.SupprimePar = acteurId;
        }

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (AccesExecution.EstConflitUnicite(ex))
        {
            return (false, $"Le code {code} est déjà utilisé par un autre projet.", Ulid.Empty);
        }

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesAxes.ModuleAudit,
            nameof(Projet), projet.Id.ToString(), avant, new { projet.Code, projet.Intitule, projet.Statut, projet.SiteId, projet.StructureId });
        return (true, null, projet.Id);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid projetId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var projet = await ctx.Projets.Include(p => p.Travaux).Include(p => p.Axe).FirstOrDefaultAsync(p => p.Id == projetId);
        if (projet is null || AxesDepense.DeCode(projet.Axe.Code) is not { } axe) return (false, "Projet introuvable.");
        var acteurId = await _acces.SaisisseurAsync(axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(axe));
        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: projet.AxeId, StructureId: projet.StructureId, SiteId: projet.SiteId));
        if (refus is not null) return (false, refus);

        // Règle § 4.3 : pas de suppression d'un élément auquel des données sont rattachées.
        if (await ctx.Dotations.AnyAsync(d => d.ProjetId == projetId))
            return (false, "Ce projet porte des dotations : il ne peut pas être supprimé (passez-le au statut Annulé ou Achevé).");
        if (await ctx.Contrats.AnyAsync(c => c.ProjetId == projetId))
            return (false, "Ce projet porte des contrats : supprimez-les d'abord.");
        if (await ctx.AvancementsPhysiques.AnyAsync(a => a.ProjetId == projetId) || await ctx.Immobilisations.AnyAsync(i => i.ProjetId == projetId))
            return (false, "Ce projet porte un avancement physique ou des immobilisations : il ne peut pas être supprimé.");

        projet.EstSupprime = true;
        projet.SupprimePar = acteurId;
        if (projet.Travaux is { } travaux)
        {
            travaux.EstSupprime = true;
            travaux.SupprimePar = acteurId;
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesAxes.ModuleAudit, nameof(Projet), projet.Id.ToString(),
            new { projet.Code, projet.Intitule }, null);
        return (true, null);
    }

    // ═════ Outils ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Le projet porte une opération de travaux routiers : toujours sur l'axe Entretien
    /// routier ; sur l'axe Investissements, quand un type de travaux est donné (réhabilitation).
    /// </summary>
    private static bool EstOperationTravaux(AxeDepense axe, SaisieProjet s)
        => axe == AxesDepense.Entretien || (axe == AxesDepense.Investissement && s.TypeTravauxId is not null);

    /// <summary>Lecteur de l'axe du projet, sinon Ulid.Empty (projet absent compris).</summary>
    private async Task<Ulid> LecteurDuProjetAsync(AppDbContext ctx, Ulid projetId)
    {
        var code = await ctx.Projets.AsNoTracking().Where(p => p.Id == projetId).Select(p => p.Axe.Code).FirstOrDefaultAsync();
        return AxesDepense.DeCode(code) is { } axe ? await _acces.LecteurAsync(axe) : Ulid.Empty;
    }

    /// <summary>Références existantes, actives et du bon type ; exigences propres à l'axe.</summary>
    private static async Task<string?> ControlerReferencesAsync(AppDbContext ctx, AxeDepense axe, SaisieProjet s)
    {
        if (s.SiteId is { } site && !await ctx.Sites.AnyAsync(x => x.Id == site && x.EstActif)) return "Site introuvable ou inactif.";
        if (s.StructureId is { } st && !await ctx.Structures.AnyAsync(x => x.Id == st && x.EstActif)) return "Service introuvable ou inactif.";
        if (s.SourceFinancementId is { } src && !await ctx.SourcesFinancement.AnyAsync(x => x.Id == src && x.EstActif))
            return "Source de financement introuvable ou inactive.";

        if (axe == AxesDepense.Investissement && s.SourceFinancementId is null)
            return "Indiquez la source de financement du projet (ressources internes ou externes).";

        if (axe == AxesDepense.Appuis)
        {
            if (s.TiersBeneficiaireId is not { } tiers) return "Indiquez l'entité bénéficiaire de l'appui.";
            if (!await ctx.Tiers.AnyAsync(x => x.Id == tiers && x.EstActif && x.TypeTiers == TypeTiers.EntiteBeneficiaireAppuis))
                return "L'entité bénéficiaire doit être un tiers actif de type « Entité bénéficiaire d'appuis ».";
        }

        if (EstOperationTravaux(axe, s))
        {
            var type = s.TypeTravauxId is { } tt
                ? await ctx.TypesTravaux.AsNoTracking().Where(x => x.Id == tt && x.EstActif && !x.EstPrestationControle).Select(x => x.Code).FirstOrDefaultAsync()
                : null;
            if (s.TypeTravauxId is not null && type is null) return "Type de travaux introuvable, inactif, ou qui est une prestation de contrôle.";
            // C.2.4 : la réhabilitation est un programme d'investissement (sous-rubrique 233, ouvrages d'infrastructure).
            if (axe == AxesDepense.Entretien && type == CodesTypeTravaux.Rehabilitation)
                return "La réhabilitation est un programme d'investissement : créez l'opération dans l'axe Investissements.";
            if (axe == AxesDepense.Investissement && type != CodesTypeTravaux.Rehabilitation)
                return "Un projet d'investissement ne porte des travaux routiers que pour une réhabilitation : choisissez ce type de travaux.";
            if (s.TypeVoieId is { } tv && !await ctx.ListesValeurs.AnyAsync(x => x.Id == tv && x.EstActif && x.Categorie == CategoriesListeValeur.TypeVoie))
                return "Type de voie introuvable ou inactif.";
            if (s.MaitreOuvrageId is { } mo && !await ctx.Tiers.AnyAsync(x => x.Id == mo && x.EstActif && x.TypeTiers == TypeTiers.MaitreOuvrage))
                return "Le maître d'ouvrage doit être un tiers actif de type « Maître d'ouvrage / administration ».";
        }
        return null;
    }
}
