using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Plateforme et habilitations (schémas plat, auth)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Organisme utilisateur (locataire) - table plat.Organisme. Racine du cloisonnement :
/// toutes les tables métier portent un OrganismeId qui pointe ici.
/// </summary>
public class Organisme : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string RaisonSociale { get; set; } = string.Empty;
    public string? Sigle { get; set; }
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? TutelleTechnique { get; set; }
    public string? TutelleFinanciere { get; set; }
    public StatutOrganisme Statut { get; set; } = StatutOrganisme.Actif;
    public DateOnly? DateActivation { get; set; }
}

/// <summary>
/// Droit d'accès d'un utilisateur sur un périmètre - table auth.Habilitation.
/// Un droit de saisie n'est actif qu'après double validation (responsable du service
/// financier et comptable + administrateur SI, deux personnes distinctes).
/// </summary>
public class Habilitation : EntiteOrganisme
{
    /// <summary>Bénéficiaire du droit.</summary>
    public Ulid UtilisateurId { get; set; }

    public TypePerimetre TypePerimetre { get; set; }

    /// <summary>
    /// Élément du périmètre (axe, programme, action, site ou structure selon
    /// <see cref="TypePerimetre"/>) ; null pour l'organisme entier. Référence polymorphe :
    /// pas de clé étrangère, cohérence vérifiée par le service des habilitations.
    /// </summary>
    public Ulid? PerimetreId { get; set; }

    public NiveauAcces NiveauAcces { get; set; }
    public DateOnly DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
    public StatutHabilitation Statut { get; set; } = StatutHabilitation.Demandee;

    public Ulid DemandeParId { get; set; }
    public Ulid? ValideurFinancierId { get; set; }
    public DateTime? DateValidationFinancier { get; set; }
    public Ulid? ValideurAdminSIId { get; set; }
    public DateTime? DateValidationAdminSI { get; set; }

    /// <summary>Motif de rejet ou de révocation.</summary>
    public string? MotifRejet { get; set; }

    public virtual Utilisateur Utilisateur { get; set; } = null!;
}

/// <summary>
/// Journal des connexions (succès, échecs, blocages) - table auth.JournalConnexion.
/// Ajout seul : aucune modification ni suppression hors purge planifiée.
/// </summary>
public class JournalConnexion
{
    public Ulid Id { get; set; }

    /// <summary>null si l'identifiant saisi ne correspond à aucun compte, ou pour un compte plateforme.</summary>
    public Ulid? OrganismeId { get; set; }
    public Ulid? UtilisateurId { get; set; }

    /// <summary>Identifiant saisi, tel quel.</summary>
    public string Login { get; set; } = string.Empty;
    public string AdresseIp { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime DateHeure { get; set; } = DateTime.UtcNow;
    public StatutConnexion Statut { get; set; }
    public string? Motif { get; set; }
}
