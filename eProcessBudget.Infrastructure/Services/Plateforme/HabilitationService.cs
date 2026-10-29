using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Plateforme;

/// <summary>Ligne de la liste des habilitations.</summary>
public sealed record LigneHabilitation(
    Ulid Id, Ulid UtilisateurId, string Beneficiaire, TypePerimetre TypePerimetre, Ulid? PerimetreId,
    NiveauAcces NiveauAcces, DateOnly DateDebut, DateOnly? DateFin, StatutHabilitation Statut,
    string? DemandePar, DateTime DateDemande,
    Ulid? ValideurFinancierId, string? ValideurFinancier, DateTime? DateValidationFinancier,
    Ulid? ValideurAdminSIId, string? ValideurAdminSI, DateTime? DateValidationAdminSI,
    string? MotifRejet)
{
    /// <summary>Libellé de l'élément du périmètre (axe, programme...), résolu après lecture.</summary>
    public string Perimetre { get; init; } = string.Empty;
}

/// <summary>Critères de la liste des habilitations.</summary>
/// <param name="EnAttenteSeulement">Uniquement les demandes en cours de validation.</param>
public sealed record FiltreHabilitations(
    StatutHabilitation? Statut = null, Ulid? UtilisateurId = null, bool EnAttenteSeulement = false,
    int Page = 1, int Taille = 20);

/// <summary>Saisie d'une demande d'habilitation.</summary>
public sealed class DemandeHabilitation
{
    public Ulid UtilisateurId { get; set; }
    public TypePerimetre TypePerimetre { get; set; } = TypePerimetre.OrganismeEntier;
    public Ulid? PerimetreId { get; set; }
    public NiveauAcces NiveauAcces { get; set; } = NiveauAcces.Saisie;
    public DateOnly DateDebut { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly? DateFin { get; set; }
}

/// <summary>Au titre de quelle fonction un validateur se prononce.</summary>
public enum RoleValidation
{
    /// <summary>Responsable du service financier et comptable.</summary>
    ServiceFinancier,
    /// <summary>Administrateur du système d'information.</summary>
    AdministrateurSI
}

/// <summary>Périmètre sur lequel une habilitation active donne accès.</summary>
public sealed record PerimetreActif(TypePerimetre TypePerimetre, Ulid? PerimetreId, NiveauAcces NiveauAcces);

/// <summary>
/// Habilitations sur un périmètre (auth.Habilitation), selon le principe des quatre yeux
/// (cahier fonctionnel § 3.1.2, annexe C) :
///
///   Demandée ──► validée par le service financier ──┐
///       │                                           ├──► Active ──► Révoquée
///       └────► validée par l'administrateur SI ─────┘
///   (toute étape en attente) ──► Rejetée
///
/// Une habilitation n'est active qu'après les DEUX validations, données par deux personnes
/// distinctes (contrôle applicatif, doublé de la contrainte CK_Habilitation_ValideursDistincts),
/// dans un ordre quelconque. Le bénéficiaire ne valide jamais sa propre habilitation.
/// Toute attribution ou tout retrait est audité.
/// </summary>
public interface IHabilitationService
{
    Task<(List<LigneHabilitation> Items, int Total)> RechercherAsync(FiltreHabilitations filtre);

    /// <summary>Éléments sélectionnables pour un type de périmètre, dans l'organisme courant.</summary>
    Task<List<ElementListe>> ListerElementsPerimetreAsync(TypePerimetre type);

    /// <summary>Utilisateurs actifs de l'organisme courant, pour l'autocomplétion.</summary>
    Task<List<ElementListe>> RechercherBeneficiairesAsync(string? texte, int max = 20);

    Task<(bool Ok, string? Erreur)> DemanderAsync(DemandeHabilitation demande);
    Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid habilitationId, RoleValidation role);
    Task<(bool Ok, string? Erreur)> RejeterAsync(Ulid habilitationId, string? motif);
    Task<(bool Ok, string? Erreur)> RevoquerAsync(Ulid habilitationId, string? motif);

    /// <summary>
    /// Périmètres actifs aujourd'hui pour un utilisateur, au niveau demandé ou supérieur.
    /// Sert de filtre de périmètre aux modules métier (cahier technique § 2.6).
    /// </summary>
    Task<IReadOnlyList<PerimetreActif>> ObtenirPerimetresActifsAsync(Ulid utilisateurId, NiveauAcces niveauMinimal);
}

public class HabilitationService : IHabilitationService
{
    private const string ModuleAudit = "Habilitations";
    private const string Lien = "/administration/habilitations";
    private const int TailleMax = 200;

    private static readonly StatutHabilitation[] StatutsEnAttente =
        [StatutHabilitation.Demandee, StatutHabilitation.ValideeFinancier, StatutHabilitation.ValideeAdminSI];

    private static readonly TypeDroit[] DroitsLecture =
        [TypeDroit.HabilitationDemander, TypeDroit.HabilitationValiderFinancier, TypeDroit.HabilitationValiderAdminSI];

    private static readonly TypeDroit[] DroitsValidation =
        [TypeDroit.HabilitationValiderFinancier, TypeDroit.HabilitationValiderAdminSI];

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IDroitService _droits;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    public HabilitationService(
        IDbContextFactory<AppDbContext> factory,
        IUserContextService utilisateurCourant,
        IDroitService droits,
        IAuditService audit,
        INotificationService notifications)
    {
        _factory = factory;
        _utilisateurCourant = utilisateurCourant;
        _droits = droits;
        _audit = audit;
        _notifications = notifications;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Lecture
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(List<LigneHabilitation> Items, int Total)> RechercherAsync(FiltreHabilitations filtre)
    {
        if (await ActeurAsync(DroitsLecture) == Ulid.Empty) return ([], 0);

        // Contexte restreint à l'organisme courant : le filtre global fait le cloisonnement.
        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Habilitations.AsNoTracking();

        if (filtre.EnAttenteSeulement) q = q.Where(h => StatutsEnAttente.Contains(h.Statut));
        else if (filtre.Statut.HasValue) q = q.Where(h => h.Statut == filtre.Statut.Value);
        if (filtre.UtilisateurId.HasValue) q = q.Where(h => h.UtilisateurId == filtre.UtilisateurId.Value);

        var total = await q.CountAsync();
        var taille = Math.Clamp(filtre.Taille, 1, TailleMax);

        var items = await q.OrderByDescending(h => h.DateCreation)
            .Skip(Math.Max(0, (filtre.Page - 1) * taille))
            .Take(taille)
            .Select(h => new LigneHabilitation(
                h.Id, h.UtilisateurId, h.Utilisateur.NomPrenoms, h.TypePerimetre, h.PerimetreId,
                h.NiveauAcces, h.DateDebut, h.DateFin, h.Statut,
                ctx.Utilisateurs.Where(u => u.Id == h.DemandeParId).Select(u => u.NomPrenoms).FirstOrDefault(),
                h.DateCreation,
                h.ValideurFinancierId,
                ctx.Utilisateurs.Where(u => u.Id == h.ValideurFinancierId).Select(u => u.NomPrenoms).FirstOrDefault(),
                h.DateValidationFinancier,
                h.ValideurAdminSIId,
                ctx.Utilisateurs.Where(u => u.Id == h.ValideurAdminSIId).Select(u => u.NomPrenoms).FirstOrDefault(),
                h.DateValidationAdminSI,
                h.MotifRejet))
            .ToListAsync();

        var libelles = await LibellesPerimetresAsync(ctx, items);
        items = items
            .Select(i => i with
            {
                Perimetre = i.PerimetreId is { } id && libelles.TryGetValue(id, out var libelle)
                    ? libelle
                    : i.TypePerimetre == TypePerimetre.OrganismeEntier ? "Tout l'organisme" : "(élément supprimé)"
            })
            .ToList();

        return (items, total);
    }

    public async Task<List<ElementListe>> ListerElementsPerimetreAsync(TypePerimetre type)
    {
        if (await ActeurAsync(DroitsLecture) == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return type switch
        {
            TypePerimetre.Axe => await ctx.Axes.AsNoTracking().OrderBy(x => x.Ordre)
                .Select(x => new ElementListe(x.Id, x.Code + " — " + x.Libelle)).ToListAsync(),
            TypePerimetre.Programme => await ctx.Programmes.AsNoTracking().Where(x => x.EstActif).OrderBy(x => x.Ordre)
                .Select(x => new ElementListe(x.Id, x.Code + " — " + x.Libelle)).ToListAsync(),
            TypePerimetre.Action => await ctx.Actions.AsNoTracking().Where(x => x.EstActif)
                .OrderBy(x => x.Programme.Ordre).ThenBy(x => x.Ordre)
                .Select(x => new ElementListe(x.Id, x.Code + " — " + x.Libelle)).ToListAsync(),
            TypePerimetre.Site => await ctx.Sites.AsNoTracking().Where(x => x.EstActif).OrderBy(x => x.Nom)
                .Select(x => new ElementListe(x.Id, x.Code + " — " + x.Nom)).ToListAsync(),
            TypePerimetre.Structure => await ctx.Structures.AsNoTracking().Where(x => x.EstActif).OrderBy(x => x.Libelle)
                .Select(x => new ElementListe(x.Id, x.Code + " — " + x.Libelle)).ToListAsync(),
            _ => []
        };
    }

    public async Task<List<ElementListe>> RechercherBeneficiairesAsync(string? texte, int max = 20)
    {
        if (await ActeurAsync(TypeDroit.HabilitationDemander) == Ulid.Empty) return [];

        var organismeId = await _utilisateurCourant.ObtenirOrganismeIdAsync();
        await using var ctx = await _factory.CreateDbContextAsync();

        var q = ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.OrganismeId == organismeId && u.EstActif && !u.EstAdministrateurPlateforme);

        if (!string.IsNullOrWhiteSpace(texte))
        {
            var t = texte.Trim();
            q = q.Where(u => u.NomPrenoms.Contains(t) || u.Matricule.Contains(t));
        }

        return await q.OrderBy(u => u.NomPrenoms)
            .Take(Math.Clamp(max, 1, 50))
            .Select(u => new ElementListe(u.Id, u.NomPrenoms + " (" + u.Matricule + ")"))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<PerimetreActif>> ObtenirPerimetresActifsAsync(Ulid utilisateurId, NiveauAcces niveauMinimal)
    {
        var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Habilitations.AsNoTracking()
            .Where(h => h.UtilisateurId == utilisateurId
                     && h.Statut == StatutHabilitation.Active
                     && h.NiveauAcces >= niveauMinimal
                     && h.DateDebut <= aujourdhui
                     && (h.DateFin == null || h.DateFin >= aujourdhui))
            .Select(h => new PerimetreActif(h.TypePerimetre, h.PerimetreId, h.NiveauAcces))
            .ToListAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Circuit
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(bool Ok, string? Erreur)> DemanderAsync(DemandeHabilitation demande)
    {
        var acteurId = await ActeurAsync(TypeDroit.HabilitationDemander);
        if (acteurId == Ulid.Empty) return (false, "Vous n'avez pas le droit de demander une habilitation.");

        var organismeId = await _utilisateurCourant.ObtenirOrganismeIdAsync();
        if (organismeId == Ulid.Empty) return (false, "Les habilitations se gèrent depuis un compte d'organisme.");

        if (!Enum.IsDefined(demande.TypePerimetre) || !Enum.IsDefined(demande.NiveauAcces))
            return (false, "Type de périmètre ou niveau d'accès inconnu.");
        if (demande.DateFin < demande.DateDebut)
            return (false, "La date de fin doit suivre la date de début.");
        if (demande.DateFin < DateOnly.FromDateTime(DateTime.UtcNow))
            return (false, "La période demandée est déjà échue.");

        if (demande.TypePerimetre == TypePerimetre.OrganismeEntier) demande.PerimetreId = null;
        else if (demande.PerimetreId is null) return (false, "Choisissez l'élément du périmètre.");

        await using var ctx = await _factory.CreateDbContextAsync();

        var beneficiaire = await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.Id == demande.UtilisateurId && u.OrganismeId == organismeId
                     && u.EstActif && !u.EstAdministrateurPlateforme)
            .Select(u => new { u.Id, u.NomPrenoms })
            .FirstOrDefaultAsync();
        if (beneficiaire is null) return (false, "Bénéficiaire introuvable dans votre organisme.");

        if (!await ElementPerimetreExisteAsync(ctx, demande.TypePerimetre, demande.PerimetreId))
            return (false, "L'élément du périmètre n'existe pas dans votre organisme.");

        // Pas de doublon : une même habilitation en cours ou active, sur une période qui chevauche.
        var doublon = await ctx.Habilitations.AnyAsync(h =>
            h.UtilisateurId == demande.UtilisateurId
            && h.TypePerimetre == demande.TypePerimetre
            && h.PerimetreId == demande.PerimetreId
            && h.NiveauAcces == demande.NiveauAcces
            && (StatutsEnAttente.Contains(h.Statut) || h.Statut == StatutHabilitation.Active)
            && (h.DateFin == null || h.DateFin >= demande.DateDebut)
            && (demande.DateFin == null || h.DateDebut <= demande.DateFin));
        if (doublon) return (false, "Une habilitation identique est déjà active ou en cours de validation sur cette période.");

        var habilitation = new Habilitation
        {
            UtilisateurId = demande.UtilisateurId,
            TypePerimetre = demande.TypePerimetre,
            PerimetreId   = demande.PerimetreId,
            NiveauAcces   = demande.NiveauAcces,
            DateDebut     = demande.DateDebut,
            DateFin       = demande.DateFin,
            Statut        = StatutHabilitation.Demandee,
            DemandeParId  = acteurId,
            CreePar       = acteurId
        };
        ctx.Habilitations.Add(habilitation);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "DEMANDE", ModuleAudit, nameof(Habilitation),
            habilitation.Id.ToString(), null, Instantane(habilitation));

        var validateurs = await DetenteursAsync(ctx, organismeId, DroitsValidation);
        await _notifications.NotifierGroupeAsync(TypeNotification.HabilitationAValider,
            $"Habilitation demandée pour {beneficiaire.NomPrenoms} : deux validations sont attendues.",
            validateurs.Where(id => id != acteurId && id != beneficiaire.Id),
            titre: "Habilitation à valider", lien: Lien,
            cleEntite: $"{nameof(Habilitation)}:{habilitation.Id}", acteurId: acteurId);

        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid habilitationId, RoleValidation role)
    {
        var droit = role == RoleValidation.ServiceFinancier
            ? TypeDroit.HabilitationValiderFinancier
            : TypeDroit.HabilitationValiderAdminSI;

        var acteurId = await ActeurAsync(droit);
        if (acteurId == Ulid.Empty)
            return (false, role == RoleValidation.ServiceFinancier
                ? "Vous n'avez pas le droit de valider au titre du service financier et comptable."
                : "Vous n'avez pas le droit de valider au titre de l'administration du SI.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var h = await ctx.Habilitations.FirstOrDefaultAsync(x => x.Id == habilitationId);
        if (h is null) return (false, "Habilitation introuvable.");

        // ── Règles des quatre yeux ───────────────────────────────────────────
        if (!StatutsEnAttente.Contains(h.Statut)) return (false, "Cette habilitation n'est plus en attente de validation.");
        if (h.UtilisateurId == acteurId) return (false, "Vous ne pouvez pas valider votre propre habilitation.");
        if (h.DateFin < DateOnly.FromDateTime(DateTime.UtcNow)) return (false, "La période de cette habilitation est échue.");

        var maintenant = DateTime.UtcNow;
        var avant = Instantane(h);

        if (role == RoleValidation.ServiceFinancier)
        {
            if (h.ValideurFinancierId is not null) return (false, "La validation du service financier est déjà donnée.");
            if (h.ValideurAdminSIId == acteurId)
                return (false, "Vous avez déjà validé au titre de l'administration du SI : la seconde validation revient à une autre personne.");
            h.ValideurFinancierId = acteurId;
            h.DateValidationFinancier = maintenant;
        }
        else
        {
            if (h.ValideurAdminSIId is not null) return (false, "La validation de l'administrateur SI est déjà donnée.");
            if (h.ValideurFinancierId == acteurId)
                return (false, "Vous avez déjà validé au titre du service financier : la seconde validation revient à une autre personne.");
            h.ValideurAdminSIId = acteurId;
            h.DateValidationAdminSI = maintenant;
        }

        var activee = h.ValideurFinancierId is not null && h.ValideurAdminSIId is not null;
        h.Statut = activee ? StatutHabilitation.Active
                 : role == RoleValidation.ServiceFinancier ? StatutHabilitation.ValideeFinancier
                 : StatutHabilitation.ValideeAdminSI;
        h.ModifiePar = acteurId;

        var erreur = await EnregistrerAsync(ctx);
        if (erreur is not null) return (false, erreur);

        await _audit.LoggerAsync(acteurId, null, activee ? "ATTRIBUTION_DROIT" : "VALIDATION", ModuleAudit,
            nameof(Habilitation), h.Id.ToString(), avant, Instantane(h),
            commentaire: role == RoleValidation.ServiceFinancier ? "Validation du service financier" : "Validation de l'administrateur SI");

        var organismeId = h.OrganismeId;
        if (activee)
        {
            await _notifications.NotifierGroupeAsync(TypeNotification.Succes,
                "Une habilitation a été activée après ses deux validations.",
                new[] { h.UtilisateurId, h.DemandeParId }.Distinct(),
                titre: "Habilitation active", lien: Lien,
                cleEntite: $"{nameof(Habilitation)}:{h.Id}", acteurId: acteurId);
        }
        else
        {
            // Il reste l'autre validation : on prévient ceux qui peuvent la donner.
            var droitRestant = role == RoleValidation.ServiceFinancier
                ? TypeDroit.HabilitationValiderAdminSI
                : TypeDroit.HabilitationValiderFinancier;
            var destinataires = await DetenteursAsync(ctx, organismeId, [droitRestant]);
            await _notifications.NotifierGroupeAsync(TypeNotification.HabilitationAValider,
                "Une habilitation attend sa seconde validation.",
                destinataires.Where(id => id != acteurId && id != h.UtilisateurId),
                titre: "Habilitation à valider", lien: Lien,
                cleEntite: $"{nameof(Habilitation)}:{h.Id}", acteurId: acteurId);
        }

        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> RejeterAsync(Ulid habilitationId, string? motif)
        => await TerminerAsync(habilitationId, motif, StatutHabilitation.Rejetee);

    public async Task<(bool Ok, string? Erreur)> RevoquerAsync(Ulid habilitationId, string? motif)
        => await TerminerAsync(habilitationId, motif, StatutHabilitation.Revoquee);

    /// <summary>Rejet (d'une demande en attente) ou révocation (d'une habilitation active).</summary>
    private async Task<(bool Ok, string? Erreur)> TerminerAsync(Ulid habilitationId, string? motif, StatutHabilitation cible)
    {
        var acteurId = await ActeurAsync(DroitsValidation);
        if (acteurId == Ulid.Empty) return (false, "Seuls les validateurs des habilitations peuvent rejeter ou révoquer.");

        motif = motif?.Trim();
        if (string.IsNullOrEmpty(motif)) return (false, "Le motif est obligatoire.");
        if (motif.Length > 500) return (false, "Le motif ne doit pas dépasser 500 caractères.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var h = await ctx.Habilitations.FirstOrDefaultAsync(x => x.Id == habilitationId);
        if (h is null) return (false, "Habilitation introuvable.");

        var etatAttendu = cible == StatutHabilitation.Rejetee
            ? StatutsEnAttente.Contains(h.Statut)
            : h.Statut == StatutHabilitation.Active;
        if (!etatAttendu)
            return (false, cible == StatutHabilitation.Rejetee
                ? "Seule une habilitation en attente de validation peut être rejetée."
                : "Seule une habilitation active peut être révoquée.");

        var avant = Instantane(h);
        h.Statut = cible;
        h.MotifRejet = motif;
        h.ModifiePar = acteurId;

        var erreur = await EnregistrerAsync(ctx);
        if (erreur is not null) return (false, erreur);

        await _audit.LoggerAsync(acteurId, null, cible == StatutHabilitation.Rejetee ? "REJET" : "RETRAIT_DROIT",
            ModuleAudit, nameof(Habilitation), h.Id.ToString(), avant, Instantane(h), commentaire: motif);

        await _notifications.NotifierGroupeAsync(TypeNotification.Avertissement,
            cible == StatutHabilitation.Rejetee
                ? $"Une demande d'habilitation a été rejetée : {motif}"
                : $"Une habilitation a été révoquée : {motif}",
            new[] { h.UtilisateurId, h.DemandeParId }.Where(id => id != acteurId).Distinct(),
            titre: cible == StatutHabilitation.Rejetee ? "Habilitation rejetée" : "Habilitation révoquée",
            lien: Lien, cleEntite: $"{nameof(Habilitation)}:{h.Id}", acteurId: acteurId);

        return (true, null);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Outils
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Utilisateur connecté s'il détient l'un des droits, sinon Ulid.Empty.</summary>
    private async Task<Ulid> ActeurAsync(params TypeDroit[] droits)
    {
        var userId = await _utilisateurCourant.ObtenirUserIdAsync();
        return await _droits.ADroitAsync(userId, droits) ? userId : Ulid.Empty;
    }

    private static async Task<string?> EnregistrerAsync(AppDbContext ctx)
    {
        try
        {
            await ctx.SaveChangesAsync();
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            return "Cette habilitation a été traitée par quelqu'un d'autre entre-temps. Rechargez la liste.";
        }
    }

    private static async Task<bool> ElementPerimetreExisteAsync(AppDbContext ctx, TypePerimetre type, Ulid? id)
        => type switch
        {
            TypePerimetre.OrganismeEntier => true,
            TypePerimetre.Axe       => await ctx.Axes.AnyAsync(x => x.Id == id),
            TypePerimetre.Programme => await ctx.Programmes.AnyAsync(x => x.Id == id),
            TypePerimetre.Action    => await ctx.Actions.AnyAsync(x => x.Id == id),
            TypePerimetre.Site      => await ctx.Sites.AnyAsync(x => x.Id == id),
            TypePerimetre.Structure => await ctx.Structures.AnyAsync(x => x.Id == id),
            _ => false
        };

    /// <summary>Libellés des éléments de périmètre d'une page de résultats : une requête par type présent.</summary>
    private static async Task<Dictionary<Ulid, string>> LibellesPerimetresAsync(AppDbContext ctx, List<LigneHabilitation> lignes)
    {
        var resultat = new Dictionary<Ulid, string>();

        foreach (var groupe in lignes.Where(l => l.PerimetreId.HasValue).GroupBy(l => l.TypePerimetre))
        {
            var ids = groupe.Select(l => l.PerimetreId!.Value).Distinct().ToList();
            var libelles = groupe.Key switch
            {
                TypePerimetre.Axe => await ctx.Axes.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new ElementListe(x.Id, x.Libelle)).ToListAsync(),
                TypePerimetre.Programme => await ctx.Programmes.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new ElementListe(x.Id, x.Code + " — " + x.Libelle)).ToListAsync(),
                TypePerimetre.Action => await ctx.Actions.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new ElementListe(x.Id, x.Code + " — " + x.Libelle)).ToListAsync(),
                TypePerimetre.Site => await ctx.Sites.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new ElementListe(x.Id, x.Nom)).ToListAsync(),
                TypePerimetre.Structure => await ctx.Structures.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new ElementListe(x.Id, x.Libelle)).ToListAsync(),
                _ => []
            };

            foreach (var e in libelles) resultat[e.Id] = e.Libelle;
        }

        return resultat;
    }

    /// <summary>
    /// Utilisateurs actifs de l'organisme qui détiennent l'un des droits. La requête trouve
    /// les candidats ; IDroitService confirme (dates de validité, retraits explicites).
    /// </summary>
    private Task<List<Ulid>> DetenteursAsync(AppDbContext ctx, Ulid organismeId, TypeDroit[] droits)
        => DetenteursDroit.ListerAsync(ctx, _droits, organismeId, droits);

    private static object Instantane(Habilitation h) => new
    {
        h.UtilisateurId, TypePerimetre = h.TypePerimetre.ToString(), h.PerimetreId,
        NiveauAcces = h.NiveauAcces.ToString(), h.DateDebut, h.DateFin, Statut = h.Statut.ToString(),
        h.ValideurFinancierId, h.ValideurAdminSIId, h.MotifRejet
    };
}
