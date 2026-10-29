namespace eProcessBudget.Core.Entities;

/// <summary>
/// Compte utilisateur (table auth.Utilisateur) : identité, mot de passe, rattachement et
/// état de connexion. Ses droits viennent de ses profils et de ses droits directs, bornés
/// par son niveau (Administrateur plateforme ou compte d'organisme).
///
/// L'utilisateur connecté s'obtient par IUserContextService (Infrastructure).
/// </summary>
public class Utilisateur : BaseEntity
{
    /// <summary>
    /// Organisme de rattachement. null uniquement pour un Administrateur plateforme
    /// (<see cref="EstAdministrateurPlateforme"/>).
    /// </summary>
    public Ulid? OrganismeId { get; set; }

    /// <summary>
    /// Identifiant de connexion (le « Login » du cahier technique). Unique, sert de clé
    /// de rapprochement.
    /// </summary>
    public string Matricule { get; set; } = string.Empty;

    public string NomPrenoms { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Fonction { get; set; }

    /// <summary>Empreinte BCrypt du mot de passe ; null tant qu'aucun mot de passe n'est défini.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Tentatives de connexion échouées consécutives (remis à 0 au succès).</summary>
    public int NbEchecsConnexion { get; set; }

    /// <summary>Fin du blocage du compte (UTC) après trop d'échecs ; null = non bloqué.</summary>
    public DateTime? BloqueJusquAu { get; set; }

    /// <summary>Dernière connexion réussie (UTC).</summary>
    public DateTime? DerniereConnexion { get; set; }

    /// <summary>Structure (service) de rattachement.</summary>
    public Ulid? StructureId { get; set; }

    /// <summary>Compte de niveau plateforme : gère les organismes, sans accès à leurs données.</summary>
    public bool EstAdministrateurPlateforme { get; set; }

    /// <summary>Un compte désactivé conserve son historique mais n'a plus aucun droit.</summary>
    public bool EstActif { get; set; } = true;

    // Navigation
    public virtual Organisme? Organisme { get; set; }
    public virtual Structure? Structure { get; set; }
    public virtual ICollection<UtilisateurProfil> UtilisateurProfils { get; set; } = new List<UtilisateurProfil>();
    public virtual ICollection<UtilisateurDroit> UtilisateurDroits { get; set; } = new List<UtilisateurDroit>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}


