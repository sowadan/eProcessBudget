using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>Saisie d'un engagement (AE ou AD) sur une dotation validée.</summary>
public sealed class SaisieEngagement
{
    public Ulid Id { get; set; }
    public Ulid? DotationId { get; set; }
    public TypeEngagement Type { get; set; } = TypeEngagement.AutorisationDepense;
    public DateOnly? Date { get; set; }
    public string Objet { get; set; } = "";
    public decimal Montant { get; set; }
    public Ulid? TiersId { get; set; }
    public Ulid? ContratId { get; set; }
}

/// <summary>Effet d'un engagement sur sa dotation, avant soumission ou validation.</summary>
public sealed record EvaluationEngagement(decimal DotationTotale, decimal AEValidees, decimal Disponible, decimal Montant, decimal Depassement, decimal? TauxApres);

public sealed record LigneFacture(Ulid Id, string Numero, DateOnly DateFacture, DateOnly DateReception, decimal Montant, bool EstPayee);
public sealed record LigneRejet(Ulid Id, string Type, DateOnly Date, decimal Montant, string Motif);
public sealed record LigneDecisionDepassement(Ulid Id, decimal Montant, decimal TauxApres, string Motif, StatutValidation Statut, DateTime? DateDecision, string? Commentaire);

/// <summary>Fiche d'un engagement : ses paiements, factures, rejets ou pénalités, échéancier des CP, dépassements.</summary>
public sealed record FicheEngagement(
    LigneEngagement Engagement, Ulid ExerciceId, int Annee, StatutExercice StatutExercice, string Imputation,
    string? Contrat, string? Origine, DateTime? DateValidation,
    List<LignePaiement> Paiements, List<LigneFacture> Factures, List<LigneRejet> Rejets,
    IReadOnlyDictionary<int, decimal> Echeancier, List<LigneDecisionDepassement> Depassements);

/// <summary>Constat d'un rejet ou d'une pénalité d'exécution, qui réduit l'encours (C.2.6).</summary>
public sealed class SaisieRejet
{
    public Ulid EngagementId { get; set; }
    public Ulid? TypePenaliteId { get; set; }
    public DateOnly? Date { get; set; }
    public decimal Montant { get; set; }
    public string Motif { get; set; } = "";
}

/// <summary>Issue d'une validation : validée, ou dépassement transmis au Responsable Budget.</summary>
public sealed record ResultatValidation(bool Ok, string? Erreur, bool DepassementSoumis = false);

/// <summary>
/// Engagements (cahier fonctionnel § 3.4.2 et 4.2, technique § 4.4).
///
/// Cycle : Brouillon → Soumis → Validé (ou Rejeté) → Annulé le cas échéant. Seul un
/// engagement validé compte dans la situation. Un dépassement de dotation n'est pas
/// bloquant : il est signalé, motivé, et l'engagement attend la décision du Responsable
/// Budget (DepassementValider) avant d'être validé.
/// </summary>
public interface IEngagementService
{
    Task<(List<LigneEngagement> Items, int Total)> ListerAsync(FiltrePieces filtre, int page = 1, int taille = 20);
    Task<FicheEngagement?> ObtenirAsync(Ulid engagementId);
    Task<SaisieEngagement?> ObtenirSaisieAsync(Ulid engagementId);

    /// <summary>Crée (en brouillon) ou modifie un engagement ; renvoie son identifiant.</summary>
    Task<(bool Ok, string? Erreur, Ulid Id)> EnregistrerAsync(SaisieEngagement saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid engagementId);

    Task<EvaluationEngagement?> EvaluerAsync(Ulid engagementId);

    /// <summary>Soumet à validation ; le motif est exigé si l'engagement dépasse la dotation.</summary>
    Task<(bool Ok, string? Erreur)> SoumettreAsync(Ulid engagementId, string? motifDepassement);
    Task<ResultatValidation> ValiderAsync(Ulid engagementId);
    Task<(bool Ok, string? Erreur)> RejeterAsync(Ulid engagementId, string motif);

    /// <summary>Annule un engagement validé sans paiement, facture ni rejet.</summary>
    Task<(bool Ok, string? Erreur)> AnnulerAsync(Ulid engagementId, string motif);

    /// <summary>Remplace l'échéancier des crédits de paiement (Σ CP ≤ montant engagé).</summary>
    Task<(bool Ok, string? Erreur)> DefinirEcheancierAsync(Ulid engagementId, IReadOnlyDictionary<int, decimal> cp);

    Task<(bool Ok, string? Erreur)> AjouterRejetAsync(SaisieRejet saisie);
    Task<(bool Ok, string? Erreur)> SupprimerRejetAsync(Ulid rejetId);

    Task<List<ElementListe>> ListerContratsAsync();
}

public sealed class EngagementService : IEngagementService
{
    private const string Prefixe = "ENG";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesExecution _acces;
    private readonly IAuditService _audit;
    private readonly IAlerteService _alertes;
    private readonly IDroitService _droits;
    private readonly INotificationService _notifications;

    public EngagementService(IDbContextFactory<AppDbContext> factory, AccesExecution acces, IAuditService audit,
                             IAlerteService alertes, IDroitService droits, INotificationService notifications)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
        _alertes = alertes;
        _droits = droits;
        _notifications = notifications;
    }

    // ═════ Lecture ════════════════════════════════════════════════════════════

    public async Task<(List<LigneEngagement> Items, int Total)> ListerAsync(FiltrePieces f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Engagements.AsNoTracking().Where(e => e.ExerciceId == f.ExerciceId);
        if (f.Statut is { } statut) q = q.Where(e => e.Statut == statut);
        if (f.Type is { } type) q = q.Where(e => e.TypeEngagement == type);
        if (f.DotationId is { } dotation) q = q.Where(e => e.DotationId == dotation);
        if (f.TiersId is { } tiers) q = q.Where(e => e.TiersId == tiers);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(e => e.Numero.Contains(r) || e.Objet.Contains(r) || e.Dotation.LigneBudgetaire.Code.Contains(r));
        }
        var total = await q.CountAsync();
        var items = await SituationService.ProjeterEngagements(ctx, q.OrderByDescending(e => e.DateEngagement).ThenByDescending(e => e.Numero)
            .Skip((page - 1) * taille).Take(taille)).ToListAsync();
        return (items, total);
    }

    public async Task<FicheEngagement?> ObtenirAsync(Ulid engagementId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        var ligne = await SituationService.ProjeterEngagements(ctx, ctx.Engagements.AsNoTracking().Where(e => e.Id == engagementId)).FirstOrDefaultAsync();
        if (ligne is null) return null;

        var entete = await ctx.Engagements.AsNoTracking().Where(e => e.Id == engagementId)
            .Select(e => new
            {
                e.ExerciceId, e.Exercice.Annee, StatutExercice = e.Exercice.Statut, e.DateValidation,
                Contrat = e.Contrat != null ? e.Contrat.Numero + " — " + e.Contrat.Objet : null,
                Origine = e.EngagementOrigine != null ? e.EngagementOrigine.Numero : null,
                Projet = e.Dotation.Projet != null ? e.Dotation.Projet.Code : null,
                Site = e.Dotation.Site != null ? e.Dotation.Site.Nom : null,
                Structure = e.Dotation.Structure != null ? e.Dotation.Structure.Code : null,
                Entite = e.Dotation.Tiers != null ? e.Dotation.Tiers.RaisonSociale : null
            })
            .FirstAsync();

        var paiements = await PaiementService.ProjeterPaiements(ctx.Paiements.AsNoTracking().Where(p => p.EngagementId == engagementId))
            .OrderByDescending(p => p.Date).ToListAsync();
        var factures = await ctx.Factures.AsNoTracking().Where(x => x.EngagementId == engagementId).OrderBy(x => x.DateFacture)
            .Select(x => new LigneFacture(x.Id, x.Numero, x.DateFacture, x.DateReception, x.Montant,
                ctx.Paiements.Any(p => p.FactureId == x.Id && p.Statut == StatutSaisie.Valide)))
            .ToListAsync();
        var rejets = await ctx.RejetsPenalites.AsNoTracking().Where(x => x.EngagementId == engagementId).OrderBy(x => x.DateConstat)
            .Select(x => new LigneRejet(x.Id, x.TypePenalite.Libelle, x.DateConstat, x.Montant, x.Motif)).ToListAsync();
        var echeancier = await ctx.EcheanciersCP.AsNoTracking().Where(x => x.EngagementId == engagementId)
            .ToDictionaryAsync(x => x.Annee, x => x.MontantCP);
        var depassements = await ctx.ValidationsDepassement.AsNoTracking().Where(v => v.EngagementId == engagementId)
            .OrderByDescending(v => v.DateCreation)
            .Select(v => new LigneDecisionDepassement(v.Id, v.MontantDepassement, v.TauxApres, v.Motif, v.Statut, v.DateDecision, v.CommentaireDecision))
            .ToListAsync();

        var imputation = string.Join(" · ", new[] { entete.Projet, entete.Site, entete.Structure, entete.Entite }.Where(x => x is not null));
        return new FicheEngagement(ligne, entete.ExerciceId, entete.Annee, entete.StatutExercice, imputation, entete.Contrat, entete.Origine,
            entete.DateValidation, paiements, factures, rejets, echeancier, depassements);
    }

    public async Task<SaisieEngagement?> ObtenirSaisieAsync(Ulid engagementId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Engagements.AsNoTracking().Where(e => e.Id == engagementId)
            .Select(e => new SaisieEngagement
            {
                Id = e.Id, DotationId = e.DotationId, Type = e.TypeEngagement, Date = e.DateEngagement, Objet = e.Objet,
                Montant = e.Montant, TiersId = e.TiersId, ContratId = e.ContratId
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<ElementListe>> ListerContratsAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Contrats.AsNoTracking().Where(c => c.Statut != StatutProjet.Annule).OrderBy(c => c.Numero)
            .Select(c => new ElementListe(c.Id, c.Numero + " — " + c.Objet)).ToListAsync();
    }

    public async Task<EvaluationEngagement?> EvaluerAsync(Ulid engagementId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == engagementId);
        return e is null ? null : await EvaluerAsync(ctx, e);
    }

    private static async Task<EvaluationEngagement> EvaluerAsync(AppDbContext ctx, Engagement e)
    {
        var m = await SituationService.MontantsAsync(ctx, e.DotationId) ?? MontantsDotation.Zero;
        // Un engagement déjà validé est déjà compté : on ne l'ajoute pas une seconde fois.
        var montant = e.Statut == StatutSaisie.Valide ? 0 : e.Montant;
        return new EvaluationEngagement(m.DotationTotale, m.AETotal, m.Disponible, e.Montant,
            CalculsExecution.Depassement(m, montant), CalculsExecution.TauxApres(m, montant));
    }

    // ═════ Saisie ═════════════════════════════════════════════════════════════

    public async Task<(bool Ok, string? Erreur, Ulid Id)> EnregistrerAsync(SaisieEngagement s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("saisir des engagements"), Ulid.Empty);

        if (s.DotationId is not { } dotationId) return (false, "Choisissez la dotation imputée.", Ulid.Empty);
        if (s.Date is not { } date) return (false, "Indiquez la date de l'engagement.", Ulid.Empty);
        var objet = s.Objet?.Trim() ?? "";
        if (objet.Length == 0) return (false, "L'objet est obligatoire.", Ulid.Empty);
        if (objet.Length > 500) return (false, "Objet : 500 caractères au plus.", Ulid.Empty);
        if (s.Montant <= 0 || !CalculsExecution.EstMontantValide(s.Montant))
            return (false, "Le montant est positif, à 2 décimales au plus.", Ulid.Empty);
        if (!Enum.IsDefined(s.Type)) return (false, "Type d'engagement inconnu.", Ulid.Empty);

        await using var ctx = await _factory.CreateDbContextAsync();
        var dotation = await ctx.Dotations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dotationId);
        if (dotation is null) return (false, "Dotation introuvable.", Ulid.Empty);
        if (dotation.Statut != StatutSaisie.Valide) return (false, "On n'engage que sur une dotation d'un exercice ouvert.", Ulid.Empty);

        var (exercice, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, dotation.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice, Ulid.Empty);
        var erreurDate = await AccesExecution.DateSaisissableAsync(ctx, exercice!, date, "de l'engagement");
        if (erreurDate is not null) return (false, erreurDate, Ulid.Empty);

        if (s.TiersId is { } t && !await ctx.Tiers.AnyAsync(x => x.Id == t && x.EstActif)) return (false, "Bénéficiaire introuvable ou inactif.", Ulid.Empty);
        if (s.ContratId is { } c && !await ctx.Contrats.AnyAsync(x => x.Id == c && x.Statut != StatutProjet.Annule))
            return (false, "Contrat introuvable ou annulé.", Ulid.Empty);
        if (s.ContratId is { } contratId && dotation.ProjetId is { } projetId && !await ctx.Contrats.AnyAsync(x => x.Id == contratId && x.ProjetId == projetId))
            return (false, "Le contrat doit relever du projet de la dotation imputée.", Ulid.Empty);

        var refus = await _acces.PerimetreAsync(ctx, acteurId, dotationId);
        if (refus is not null) return (false, refus, Ulid.Empty);

        Engagement engagement;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            engagement = new Engagement { ExerciceId = dotation.ExerciceId, CreePar = acteurId };
            ctx.Engagements.Add(engagement);
        }
        else
        {
            var existant = await ctx.Engagements.FirstOrDefaultAsync(e => e.Id == s.Id);
            if (existant is null) return (false, "Engagement introuvable.", Ulid.Empty);
            if (existant.EstReport) return (false, "Un encours reporté ne se modifie pas.", Ulid.Empty);
            if (existant.Statut is not (StatutSaisie.Brouillon or StatutSaisie.Rejete))
                return (false, "Seul un engagement en brouillon ou rejeté se modifie.", Ulid.Empty);
            if (existant.ExerciceId != dotation.ExerciceId) return (false, "Un engagement ne change pas d'exercice.", Ulid.Empty);
            // Changer de dotation exige aussi le périmètre de celle qu'il quitte.
            if (existant.DotationId != dotationId && await _acces.PerimetreAsync(ctx, acteurId, existant.DotationId) is { } refusOrigine)
                return (false, refusOrigine, Ulid.Empty);
            if (s.Montant < (await ctx.EcheanciersCP.Where(x => x.EngagementId == existant.Id).SumAsync(x => (decimal?)x.MontantCP) ?? 0))
                return (false, "Le montant ne peut pas descendre sous le total de l'échéancier des CP.", Ulid.Empty);
            engagement = existant;
            avant = new { engagement.DotationId, engagement.Montant, engagement.Statut };
        }

        engagement.DotationId = dotationId;
        engagement.TypeEngagement = s.Type;
        engagement.DateEngagement = date;
        engagement.Objet = objet;
        engagement.Montant = s.Montant;
        engagement.TiersId = s.TiersId;
        engagement.ContratId = s.ContratId;
        engagement.Statut = StatutSaisie.Brouillon;
        engagement.ModifiePar = acteurId;

        var erreur = await EnregistrerNumeroteAsync(ctx, engagement, exercice!.Annee);
        if (erreur is not null) return (false, erreur, Ulid.Empty);

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesExecution.ModuleAudit,
            nameof(Engagement), engagement.Id.ToString(), avant, new { engagement.Numero, engagement.DotationId, engagement.Montant });
        return (true, null, engagement.Id);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid engagementId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("supprimer un engagement"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.FirstOrDefaultAsync(x => x.Id == engagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut is not (StatutSaisie.Brouillon or StatutSaisie.Rejete)) return (false, "Seul un engagement en brouillon ou rejeté se supprime ; un engagement validé s'annule.");
        var refus = await _acces.PerimetreAsync(ctx, acteurId, e.DotationId);
        if (refus is not null) return (false, refus);

        foreach (var cp in await ctx.EcheanciersCP.Where(x => x.EngagementId == engagementId).ToListAsync())
        {
            cp.EstSupprime = true;
            cp.SupprimePar = acteurId;
        }
        e.EstSupprime = true;
        e.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesExecution.ModuleAudit, nameof(Engagement), e.Id.ToString(),
            new { e.Numero, e.Montant }, null);
        return (true, null);
    }

    // ═════ Cycle de validation ════════════════════════════════════════════════

    public async Task<(bool Ok, string? Erreur)> SoumettreAsync(Ulid engagementId, string? motifDepassement)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("soumettre un engagement"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.FirstOrDefaultAsync(x => x.Id == engagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut != StatutSaisie.Brouillon) return (false, "Seul un engagement en brouillon se soumet.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, e.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var refus = await _acces.PerimetreAsync(ctx, acteurId, e.DotationId);
        if (refus is not null) return (false, refus);

        var evaluation = await EvaluerAsync(ctx, e);
        var motif = motifDepassement?.Trim() ?? "";
        if (evaluation.Depassement > 0)
        {
            if (motif.Length == 0)
                return (false, $"Cet engagement dépasse la dotation de {evaluation.Depassement:N2} : indiquez le motif du dépassement.");
            if (motif.Length > 1000) return (false, "Motif : 1 000 caractères au plus.");
            ctx.ValidationsDepassement.Add(NouveauDepassement(e, evaluation, motif, acteurId));
        }

        e.Statut = StatutSaisie.Soumis;
        e.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SOUMISSION", AccesExecution.ModuleAudit, nameof(Engagement), e.Id.ToString(),
            null, new { e.Numero, e.Montant, evaluation.Depassement }, commentaire: evaluation.Depassement > 0 ? motif : null);

        if (evaluation.Depassement > 0)
            await NotifierDepassementAsync(ctx, e, evaluation, acteurId);
        else
        {
            var valideurs = await DetenteursDroit.ListerAsync(ctx, _droits, ctx.OrganismeCourantId, TypeDroit.EngagementValider);
            await _notifications.NotifierGroupeAsync(TypeNotification.ActionRequise,
                $"L'engagement {e.Numero} ({e.Montant:N2}) attend votre validation.", valideurs.Where(v => v != acteurId),
                titre: "Engagement à valider", lien: $"/execution/engagements/{e.Id}", cleEntite: $"Engagement:{e.Id}", acteurId: acteurId);
        }
        return (true, null);
    }

    public async Task<ResultatValidation> ValiderAsync(Ulid engagementId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementValider);
        if (acteurId == Ulid.Empty) return new(false, AccesExecution.MessageDroit("valider les engagements"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.FirstOrDefaultAsync(x => x.Id == engagementId);
        if (e is null) return new(false, "Engagement introuvable.");
        if (e.Statut != StatutSaisie.Soumis) return new(false, "Seul un engagement soumis se valide.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, e.ExerciceId);
        if (erreurExercice is not null) return new(false, erreurExercice);

        var decisions = await ctx.ValidationsDepassement.Where(v => v.EngagementId == engagementId).Select(v => v.Statut).ToListAsync();
        if (decisions.Contains(StatutValidation.EnAttente))
            return new(false, "Le dépassement de cet engagement attend la décision du Responsable Budget (file des dépassements).");

        // La situation a pu changer depuis la soumission : le dépassement se constate ici.
        var evaluation = await EvaluerAsync(ctx, e);
        if (evaluation.Depassement > 0 && !decisions.Contains(StatutValidation.Validee))
        {
            ctx.ValidationsDepassement.Add(NouveauDepassement(e, evaluation,
                "Dépassement constaté à la validation : la situation de la dotation a évolué depuis la soumission.", acteurId));
            await ctx.SaveChangesAsync();
            await _audit.LoggerAsync(acteurId, null, "DEPASSEMENT_SIGNALE", AccesExecution.ModuleAudit, nameof(Engagement), e.Id.ToString(),
                null, new { e.Numero, evaluation.Depassement });
            await NotifierDepassementAsync(ctx, e, evaluation, acteurId);
            return new(true, null, DepassementSoumis: true);
        }

        e.Statut = StatutSaisie.Valide;
        e.ValideParId = acteurId;
        e.DateValidation = DateTime.UtcNow;
        e.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "VALIDATION", AccesExecution.ModuleAudit, nameof(Engagement), e.Id.ToString(),
            null, new { e.Numero, e.DotationId, e.Montant });
        await _notifications.NotifierAsync(TypeNotification.Succes, $"L'engagement {e.Numero} est validé.", e.CreePar ?? Ulid.Empty,
            titre: "Engagement validé", lien: $"/execution/engagements/{e.Id}", acteurId: acteurId);
        await _alertes.VerifierAsync([e.DotationId]);
        return new(true, null);
    }

    public async Task<(bool Ok, string? Erreur)> RejeterAsync(Ulid engagementId, string motif)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementValider, TypeDroit.DepassementValider);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("rejeter les engagements"));
        motif = motif?.Trim() ?? "";
        if (motif.Length == 0) return (false, "Indiquez le motif du rejet.");
        if (motif.Length > 1000) return (false, "Motif : 1 000 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.FirstOrDefaultAsync(x => x.Id == engagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut != StatutSaisie.Soumis) return (false, "Seul un engagement soumis se rejette.");

        await TerminerAsync(ctx, e, StatutSaisie.Rejete, acteurId, motif);
        await _audit.LoggerAsync(acteurId, null, "REJET", AccesExecution.ModuleAudit, nameof(Engagement), e.Id.ToString(),
            null, new { e.Numero, e.Montant }, commentaire: motif);
        await _notifications.NotifierAsync(TypeNotification.Avertissement, $"L'engagement {e.Numero} est rejeté : {motif}", e.CreePar ?? Ulid.Empty,
            titre: "Engagement rejeté", lien: $"/execution/engagements/{e.Id}", acteurId: acteurId);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> AnnulerAsync(Ulid engagementId, string motif)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementValider);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("annuler un engagement"));
        motif = motif?.Trim() ?? "";
        if (motif.Length == 0) return (false, "Indiquez le motif de l'annulation.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.FirstOrDefaultAsync(x => x.Id == engagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut != StatutSaisie.Valide) return (false, "Seul un engagement validé s'annule.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, e.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        if (await ctx.Paiements.AnyAsync(p => p.EngagementId == engagementId && p.Statut == StatutSaisie.Valide)
            || await ctx.Factures.AnyAsync(f => f.EngagementId == engagementId)
            || await ctx.RejetsPenalites.AnyAsync(r => r.EngagementId == engagementId))
            return (false, "Cet engagement porte des paiements, des factures ou des rejets : il ne peut pas être annulé.");

        e.Statut = StatutSaisie.Annule;
        e.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "ANNULATION", AccesExecution.ModuleAudit, nameof(Engagement), e.Id.ToString(),
            null, new { e.Numero, e.Montant }, commentaire: motif);
        return (true, null);
    }

    /// <summary>Clôt un engagement soumis (rejet, ou décision sur dépassement) et ses demandes en attente.</summary>
    internal static async Task TerminerAsync(AppDbContext ctx, Engagement e, StatutSaisie statut, Ulid acteurId, string commentaire)
    {
        foreach (var v in await ctx.ValidationsDepassement.Where(v => v.EngagementId == e.Id && v.Statut == StatutValidation.EnAttente).ToListAsync())
        {
            v.Statut = StatutValidation.Rejetee;
            v.DecideParId = acteurId;
            v.DateDecision = DateTime.UtcNow;
            v.CommentaireDecision = commentaire;
            v.ModifiePar = acteurId;
        }
        e.Statut = statut;
        e.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();
    }

    private static ValidationDepassement NouveauDepassement(Engagement e, EvaluationEngagement evaluation, string motif, Ulid acteurId) => new()
    {
        DotationId = e.DotationId,
        EngagementId = e.Id,
        MontantDepassement = evaluation.Depassement,
        // DECIMAL(7,4) : un taux démesuré (dotation quasi nulle) est plafonné.
        TauxApres = Math.Min(evaluation.TauxApres ?? 999.9999m, 999.9999m),
        Motif = motif,
        Statut = StatutValidation.EnAttente,
        CreePar = acteurId
    };

    private async Task NotifierDepassementAsync(AppDbContext ctx, Engagement e, EvaluationEngagement evaluation, Ulid acteurId)
    {
        var decideurs = await DetenteursDroit.ListerAsync(ctx, _droits, ctx.OrganismeCourantId, TypeDroit.DepassementValider);
        await _notifications.NotifierGroupeAsync(TypeNotification.DepassementDotation,
            $"L'engagement {e.Numero} ({e.Montant:N2}) dépasse sa dotation de {evaluation.Depassement:N2} : votre décision est attendue.",
            decideurs, titre: "Dépassement de dotation", lien: "/execution/depassements", cleEntite: $"Engagement:{e.Id}", acteurId: acteurId);
    }

    // ═════ Échéancier des CP, rejets et pénalités ═════════════════════════════

    public async Task<(bool Ok, string? Erreur)> DefinirEcheancierAsync(Ulid engagementId, IReadOnlyDictionary<int, decimal> cp)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("définir l'échéancier des CP"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.Include(x => x.Exercice).FirstOrDefaultAsync(x => x.Id == engagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut is StatutSaisie.Annule or StatutSaisie.Rejete) return (false, "Engagement annulé ou rejeté.");
        if (e.Exercice.Statut != StatutExercice.Ouvert) return (false, "La saisie exige un exercice ouvert.");
        var refus = await _acces.PerimetreAsync(ctx, acteurId, e.DotationId);
        if (refus is not null) return (false, refus);

        var annee = e.Exercice.Annee;
        foreach (var (an, montant) in cp)
        {
            if (an < annee || an > annee + 10) return (false, $"L'année {an} sort de la période admise ({annee} à {annee + 10}).");
            if (montant <= 0 || !CalculsExecution.EstMontantValide(montant)) return (false, $"CP {an} : montant positif, à 2 décimales au plus.");
        }
        if (cp.Values.Sum() > e.Montant)
            return (false, $"Le total des CP ({cp.Values.Sum():N2}) dépasse le montant engagé ({e.Montant:N2}).");

        var existants = await ctx.EcheanciersCP.Where(x => x.EngagementId == engagementId).ToListAsync();
        foreach (var x in existants.Where(x => !cp.ContainsKey(x.Annee)))
        {
            x.EstSupprime = true;
            x.SupprimePar = acteurId;
        }
        foreach (var (an, montant) in cp)
        {
            var ligne = existants.FirstOrDefault(x => x.Annee == an);
            if (ligne is null) ctx.EcheanciersCP.Add(new EcheancierCP { EngagementId = engagementId, Annee = an, MontantCP = montant, CreePar = acteurId });
            else if (ligne.MontantCP != montant) { ligne.MontantCP = montant; ligne.ModifiePar = acteurId; }
        }
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "MODIFICATION", AccesExecution.ModuleAudit, nameof(EcheancierCP), engagementId.ToString(),
            existants.ToDictionary(x => x.Annee, x => x.MontantCP), cp);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> AjouterRejetAsync(SaisieRejet s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("constater un rejet ou une pénalité"));
        if (s.TypePenaliteId is not { } typeId) return (false, "Choisissez le type de rejet ou de pénalité.");
        if (s.Date is not { } date) return (false, "Indiquez la date du constat.");
        if (s.Montant <= 0 || !CalculsExecution.EstMontantValide(s.Montant)) return (false, "Le montant est positif, à 2 décimales au plus.");
        var motif = s.Motif?.Trim() ?? "";
        if (motif.Length == 0) return (false, "Le motif est obligatoire.");
        if (motif.Length > 500) return (false, "Motif : 500 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.EngagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut != StatutSaisie.Valide) return (false, "Un rejet ou une pénalité se constate sur un engagement validé.");
        var (exercice, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, e.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var erreurDate = await AccesExecution.DateSaisissableAsync(ctx, exercice!, date, "du constat");
        if (erreurDate is not null) return (false, erreurDate);
        if (!await ctx.ListesValeurs.AnyAsync(l => l.Id == typeId && l.Categorie == CategoriesListeValeur.TypePenalite && l.EstActif))
            return (false, "Type de pénalité introuvable ou inactif.");
        var refus = await _acces.PerimetreAsync(ctx, acteurId, e.DotationId);
        if (refus is not null) return (false, refus);

        var reste = await ResteAPayerAsync(ctx, e);
        if (s.Montant > reste) return (false, $"Le montant dépasse le reste à payer de l'engagement ({reste:N2}).");

        var rejet = new RejetPenalite { EngagementId = e.Id, TypePenaliteId = typeId, DateConstat = date, Montant = s.Montant, Motif = motif, CreePar = acteurId };
        ctx.RejetsPenalites.Add(rejet);
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "CREATION", AccesExecution.ModuleAudit, nameof(RejetPenalite), rejet.Id.ToString(),
            null, new { e.Numero, rejet.Montant, rejet.Motif });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerRejetAsync(Ulid rejetId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("supprimer un rejet ou une pénalité"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var r = await ctx.RejetsPenalites.Include(x => x.Engagement).FirstOrDefaultAsync(x => x.Id == rejetId);
        if (r is null) return (false, "Rejet ou pénalité introuvable.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, r.Engagement.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var refus = await _acces.PerimetreAsync(ctx, acteurId, r.Engagement.DotationId);
        if (refus is not null) return (false, refus);

        r.EstSupprime = true;
        r.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesExecution.ModuleAudit, nameof(RejetPenalite), r.Id.ToString(),
            new { r.Engagement.Numero, r.Montant }, null);
        return (true, null);
    }

    /// <summary>Montant engagé − paiements validés − rejets ou pénalités.</summary>
    internal static async Task<decimal> ResteAPayerAsync(AppDbContext ctx, Engagement e)
    {
        var paye = await ctx.Paiements.Where(p => p.EngagementId == e.Id && p.Statut == StatutSaisie.Valide).SumAsync(p => (decimal?)p.Montant) ?? 0;
        var rejets = await ctx.RejetsPenalites.Where(r => r.EngagementId == e.Id).SumAsync(r => (decimal?)r.Montant) ?? 0;
        return CalculsExecution.ResteAPayer(e.Montant, paye, rejets);
    }

    /// <summary>Attribue le numéro (ENG-année-rang) d'un nouvel engagement et enregistre ; une collision de numéro est retentée.</summary>
    private static async Task<string?> EnregistrerNumeroteAsync(AppDbContext ctx, Engagement e, int annee)
    {
        var nouveau = string.IsNullOrEmpty(e.Numero);
        for (var essai = 0; ; essai++)
        {
            if (nouveau)
                e.Numero = await AccesExecution.NumeroSuivantAsync(
                    ctx.Engagements.IgnoreQueryFilters().Where(x => x.OrganismeId == ctx.OrganismeCourantId).Select(x => x.Numero), Prefixe, annee);
            try
            {
                await ctx.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException ex) when (nouveau && essai < 3 && AccesExecution.EstConflitUnicite(ex))
            {
            }
        }
    }
}
