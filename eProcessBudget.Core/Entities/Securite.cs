using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  MODÈLE DE DROITS
//
//  Utilisateur --< UtilisateurProfil >-- Profil --< ProfilDroit >-- TypeDroit
//       +--------< UtilisateurDroit >--------------------------- TypeDroit
//
//  Deux chemins pour obtenir un droit :
//    1. via un PROFIL   - cas normal, réutilisable (Gestionnaire, Consultant...)
//    2. en DIRECT       - exception ponctuelle, avec date de fin et justification
//
//  Un UtilisateurDroit avec EstAccorde = false RETIRE un droit hérité d'un profil.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Ensemble de droits réutilisable, attribuable à plusieurs utilisateurs.</summary>
public class Profil : BaseEntity
{
    /// <summary>Code technique stable (ex. ADMIN, GESTIONNAIRE). Unique.</summary>
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Profil livré par le socle : non supprimable depuis l'interface.</summary>
    public bool EstSysteme { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual ICollection<ProfilDroit> Droits { get; set; } = new List<ProfilDroit>();
    public virtual ICollection<UtilisateurProfil> UtilisateurProfils { get; set; } = new List<UtilisateurProfil>();
}

/// <summary>Un droit accordé par un profil.</summary>
public class ProfilDroit : BaseEntity
{
    public Ulid ProfilId { get; set; }
    public TypeDroit TypeDroit { get; set; }

    public virtual Profil Profil { get; set; } = null!;
}

/// <summary>Attribution d'un profil à un utilisateur, éventuellement à durée limitée.</summary>
public class UtilisateurProfil : BaseEntity
{
    public Ulid UtilisateurId { get; set; }
    public Ulid ProfilId { get; set; }

    public DateTime DateDebut { get; set; } = DateTime.UtcNow;
    /// <summary>null = sans limite de durée.</summary>
    public DateTime? DateFin { get; set; }
    public Ulid? AttribuePar { get; set; }

    public virtual Utilisateur Utilisateur { get; set; } = null!;
    public virtual Profil Profil { get; set; } = null!;
}

/// <summary>
/// Droit accordé (ou retiré) directement à un utilisateur, hors profil.
/// Sert aux exceptions : intérim, mission temporaire, retrait ciblé.
/// </summary>
public class UtilisateurDroit : BaseEntity
{
    public Ulid UtilisateurId { get; set; }
    public TypeDroit TypeDroit { get; set; }

    /// <summary>true = ajoute le droit ; false = le retire même s'il vient d'un profil.</summary>
    public bool EstAccorde { get; set; } = true;

    public string? Justification { get; set; }
    public DateTime DateDebut { get; set; } = DateTime.UtcNow;
    public DateTime? DateFin { get; set; }
    public Ulid? AttribuePar { get; set; }

    public virtual Utilisateur Utilisateur { get; set; } = null!;
}

// ═══════════════════════════════════════════════════════════════════════════════
//  TRAÇABILITÉ
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Journal d'audit applicatif : qui a fait quoi, quand, sur quelle entité.
/// Table aud.JournalAudit, en ajout seul : aucun service n'expose de modification ni de
/// suppression (hors purge planifiée selon la rétention de l'organisme).
/// </summary>
public class AuditLog
{
    public Ulid Id { get; set; }

    /// <summary>Organisme concerné ; null pour une action de niveau plateforme.</summary>
    public Ulid? OrganismeId { get; set; }

    public Ulid? UtilisateurId { get; set; }
    public string? UtilisateurMatricule { get; set; }

    /// <summary>Verbe métier en majuscules : CREATION, MODIFICATION, SUPPRESSION...</summary>
    public string Action { get; set; } = string.Empty;
    /// <summary>Domaine fonctionnel : Securite, Notifications...</summary>
    public string Module { get; set; } = string.Empty;
    /// <summary>Nom de l'entité concernée.</summary>
    public string Entite { get; set; } = string.Empty;
    public string? EntiteId { get; set; }

    /// <summary>États sérialisés en JSON (null si non pertinent).</summary>
    public string? AncienneValeur { get; set; }
    public string? NouvelleValeur { get; set; }

    public string? Commentaire { get; set; }
    public DateTime DateAction { get; set; } = DateTime.UtcNow;
    public string? AdresseIP { get; set; }
    public string? UserAgent { get; set; }
    public bool EstSucces { get; set; } = true;
    public string? MessageErreur { get; set; }

    /// <summary>Identifiant de la requête ou du circuit, pour relier les lignes d'une même opération.</summary>
    public string? CorrelationId { get; set; }
}
