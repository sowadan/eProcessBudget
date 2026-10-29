using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;

namespace eProcessBudget.Infrastructure.Services.Notifications;

/// <summary>
/// Stockage des notifications. Implémentation : NotificationStoreEf (table ntf.Notification).
///
/// NotificationService, la cloche et la page /notifications ne connaissent que cette
/// interface. Voir docs/06-notifications.md.
/// </summary>
public interface INotificationStore
{
    /// <summary>Persiste une notification et renvoie l'objet avec son Id attribué.</summary>
    Task<Notification> AjouterAsync(Notification notification);

    /// <summary>Persiste un lot en une fois (envoi à un groupe).</summary>
    Task<List<Notification>> AjouterLotAsync(IEnumerable<Notification> notifications);

    Task<int> CompterNonLuesAsync(Ulid utilisateurId);

    /// <summary>Les N plus récentes, pour le volet de la cloche.</summary>
    Task<List<Notification>> ObtenirRecentesAsync(Ulid utilisateurId, int max = 8, bool uniquementNonLues = false);

    /// <summary>Page filtrée pour l'écran /notifications.</summary>
    Task<(List<Notification> Items, int Total, int NonLues)> ObtenirPageAsync(
        Ulid utilisateurId, int page = 1, int taille = 20,
        bool? estLue = null, TypeNotification? type = null, string? recherche = null);

    Task<int> MarquerLueAsync(Ulid id, Ulid utilisateurId);
    Task<int> MarquerNonLueAsync(Ulid id, Ulid utilisateurId);
    Task<int> MarquerToutesLuesAsync(Ulid utilisateurId);

    Task<int> SupprimerAsync(Ulid id, Ulid utilisateurId);
    Task<int> SupprimerLuesAsync(Ulid utilisateurId);
}


