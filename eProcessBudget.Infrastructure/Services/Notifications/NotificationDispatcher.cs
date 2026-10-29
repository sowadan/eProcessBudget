using System.Collections.Concurrent;
using eProcessBudget.Core.Enums;

namespace eProcessBudget.Infrastructure.Services.Notifications;

/// <summary>Charge utile poussée en temps réel vers le circuit Blazor du destinataire.</summary>
public record NotificationTempsReel(
    Ulid UtilisateurId,
    Ulid NotificationId,
    TypeNotification Type,
    string? Titre,
    string Message,
    string? Lien,
    DateTime DateCreation);

/// <summary>
/// Bus de notifications en mémoire (publication / abonnement).
///
/// Permet à la cloche d'un utilisateur connecté d'afficher une notification à l'instant
/// où elle est créée, sans interroger le stockage en boucle. Le composant conserve tout
/// de même un rafraîchissement périodique de secours.
///
/// LIMITE ASSUMÉE : la diffusion est locale au processus. En déploiement multi-instances,
/// un utilisateur connecté à l'instance A ne reçoit pas en push une notification créée par
/// l'instance B - le rafraîchissement de secours la ramènera. Pour du push strict en
/// multi-instances, branchez ici un backplane (Redis, Azure SignalR) : l'interface ne bouge pas.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Abonne un gestionnaire aux notifications d'un utilisateur. Disposer pour se désabonner.</summary>
    IDisposable Souscrire(Ulid utilisateurId, Func<NotificationTempsReel, Task> gestionnaire);

    /// <summary>Diffuse une notification aux abonnés de l'utilisateur ciblé.</summary>
    Task PublierAsync(NotificationTempsReel notification);
}

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly ConcurrentDictionary<Ulid, List<Func<NotificationTempsReel, Task>>> _abonnes = new();
    private readonly object _verrou = new();

    public IDisposable Souscrire(Ulid utilisateurId, Func<NotificationTempsReel, Task> gestionnaire)
    {
        lock (_verrou)
        {
            var liste = _abonnes.GetOrAdd(utilisateurId, _ => new List<Func<NotificationTempsReel, Task>>());
            liste.Add(gestionnaire);
        }
        return new Souscription(this, utilisateurId, gestionnaire);
    }

    public async Task PublierAsync(NotificationTempsReel notification)
    {
        List<Func<NotificationTempsReel, Task>> gestionnaires;

        lock (_verrou)
        {
            if (!_abonnes.TryGetValue(notification.UtilisateurId, out var liste) || liste.Count == 0)
                return;
            gestionnaires = liste.ToList();   // copie : on itère hors verrou
        }

        foreach (var gestionnaire in gestionnaires)
        {
            // Un abonné défaillant (circuit fermé entre-temps) ne doit pas priver les autres.
            try { await gestionnaire(notification); }
            catch { /* best-effort : la notification reste dans le stockage */ }
        }
    }

    private void Desabonner(Ulid utilisateurId, Func<NotificationTempsReel, Task> gestionnaire)
    {
        lock (_verrou)
        {
            if (!_abonnes.TryGetValue(utilisateurId, out var liste)) return;
            liste.Remove(gestionnaire);
            if (liste.Count == 0) _abonnes.TryRemove(utilisateurId, out _);
        }
    }

    private sealed class Souscription : IDisposable
    {
        private readonly NotificationDispatcher _dispatcher;
        private readonly Ulid _utilisateurId;
        private readonly Func<NotificationTempsReel, Task> _gestionnaire;
        private bool _dispose;

        public Souscription(NotificationDispatcher dispatcher, Ulid utilisateurId,
                            Func<NotificationTempsReel, Task> gestionnaire)
        {
            _dispatcher = dispatcher;
            _utilisateurId = utilisateurId;
            _gestionnaire = gestionnaire;
        }

        public void Dispose()
        {
            if (_dispose) return;
            _dispose = true;
            _dispatcher.Desabonner(_utilisateurId, _gestionnaire);
        }
    }
}


