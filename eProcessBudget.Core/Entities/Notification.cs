using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

/// <summary>
/// Notification destinée à un utilisateur : affichée dans la cloche de l'en-tête et
/// dans la page /notifications.
///
/// Structure volontairement générique. Pour rattacher une notification à vos propres
/// entités métier, utilisez <see cref="CleEntite"/> (ex. "Commande:42") plutôt que
/// d'ajouter une clé étrangère par entité - voir docs/06-notifications.md.
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>Organisme concerné ; null pour une notification de niveau plateforme.</summary>
    public Ulid? OrganismeId { get; set; }

    /// <summary>Destinataire.</summary>
    public Ulid UtilisateurId { get; set; }

    public TypeNotification Type { get; set; }

    /// <summary>Titre court affiché en gras (si vide, le libellé du type est utilisé).</summary>
    public string? Titre { get; set; }
    public string Message { get; set; } = string.Empty;

    public bool EstLue { get; set; }
    public DateTime? DateLecture { get; set; }

    /// <summary>Lien relatif ouvert au clic (ex. "/commandes/12").</summary>
    public string? Lien { get; set; }

    /// <summary>
    /// Référence libre vers l'objet métier concerné, au format "Entite:Id" (ex.
    /// "Engagement:01J..."). Tient lieu des champs EntiteNom / EntiteId du cahier technique.
    /// </summary>
    public string? CleEntite { get; set; }

    /// <summary>Auteur de l'action à l'origine de la notification (null = système).</summary>
    public Ulid? ActeurId { get; set; }

    public virtual Utilisateur Utilisateur { get; set; } = null!;
}

/// <summary>
/// Réglage d'acheminement par type de notification : une ligne par valeur de
/// <see cref="TypeNotification"/>. Permet à un administrateur d'activer ou de désactiver
/// indépendamment la cloche interne et l'email pour chaque événement.
/// </summary>
public class ParametreNotification : BaseEntity
{
    public TypeNotification Type { get; set; }

    /// <summary>Notification interne : persistée + poussée en temps réel dans la cloche.</summary>
    public bool CanalInterneActif { get; set; } = true;

    /// <summary>Envoi d'un email au destinataire.</summary>
    public bool CanalEmailActif { get; set; }
}


