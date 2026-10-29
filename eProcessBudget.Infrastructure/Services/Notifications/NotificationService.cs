using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.Extensions.Options;

namespace eProcessBudget.Infrastructure.Services.Notifications;

/// <summary>
/// API des notifications utilisée par le reste de l'application.
///
/// UTILISATION TYPIQUE, depuis un service ou une page :
///
///     await _notifications.NotifierAsync(
///         TypeNotification.ActionRequise,
///         "La commande C-2024-018 attend votre validation.",
///         destinataireId: responsableId,
///         titre: "Validation requise",
///         lien: "/commandes/18",
///         acteurId: moi);
///
/// Ce service ne sait PAS où les notifications sont stockées : il délègue à
/// <see cref="INotificationStore"/>. C'est ce découplage qui permet de démarrer sans base
/// puis de brancher la persistance sans rien réécrire.
/// </summary>
public interface INotificationService
{
    /// <summary>Notifie un utilisateur. Renvoie la notification créée, ou null si le canal interne est désactivé.</summary>
    Task<Notification?> NotifierAsync(
        TypeNotification type,
        string message,
        Ulid destinataireId,
        string? titre = null,
        string? lien = null,
        string? cleEntite = null,
        Ulid? acteurId = null);

    /// <summary>Notifie plusieurs utilisateurs en une opération.</summary>
    Task NotifierGroupeAsync(
        TypeNotification type,
        string message,
        IEnumerable<Ulid> destinatairesIds,
        string? titre = null,
        string? lien = null,
        string? cleEntite = null,
        Ulid? acteurId = null);

    // ── Lecture, utilisée par la cloche et la page /notifications ────────────
    Task<int> CompterNonLuesAsync(Ulid utilisateurId);
    Task<List<Notification>> ObtenirRecentesAsync(Ulid utilisateurId, int max = 8, bool uniquementNonLues = false);
    Task<(List<Notification> Items, int Total, int NonLues)> ObtenirPageAsync(
        Ulid utilisateurId, int page = 1, int taille = 20,
        bool? estLue = null, TypeNotification? type = null, string? recherche = null);

    Task MarquerLueAsync(Ulid id, Ulid utilisateurId);
    Task MarquerNonLueAsync(Ulid id, Ulid utilisateurId);
    Task MarquerToutesLuesAsync(Ulid utilisateurId);
    Task<int> SupprimerAsync(Ulid id, Ulid utilisateurId);
    Task<int> SupprimerLuesAsync(Ulid utilisateurId);

}

public class NotificationService : INotificationService
{
    private readonly INotificationStore _store;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IParametreNotificationService _parametres;
    private readonly IEmailQueue _emails;
    private readonly IUtilisateurStore _utilisateurs;
    private readonly EmailOptions _emailOptions;
    private readonly ApplicationOptions _application;

    public NotificationService(
        INotificationStore store,
        INotificationDispatcher dispatcher,
        IParametreNotificationService parametres,
        IEmailQueue emails,
        IUtilisateurStore utilisateurs,
        IOptions<EmailOptions> emailOptions,
        IOptions<ApplicationOptions> application)
    {
        _store = store;
        _dispatcher = dispatcher;
        _parametres = parametres;
        _emails = emails;
        _utilisateurs = utilisateurs;
        _emailOptions = emailOptions.Value;
        _application = application.Value;
    }


    public async Task<Notification?> NotifierAsync(
        TypeNotification type, string message, Ulid destinataireId,
        string? titre = null, string? lien = null, string? cleEntite = null, Ulid? acteurId = null)
    {
        if (destinataireId == Ulid.Empty) return null;

        var canaux = await _parametres.ObtenirCanauxAsync(type);
        Notification? creee = null;

        if (canaux.Interne)
        {
            creee = await _store.AjouterAsync(new Notification
            {
                UtilisateurId = destinataireId,
                Type          = type,
                Titre         = titre,
                Message       = message,
                Lien          = lien,
                CleEntite     = cleEntite,
                ActeurId      = acteurId,
                EstLue        = false,
                DateCreation  = DateTime.UtcNow
            });

            // Push immédiat vers la cloche si le destinataire est connecté.
            await _dispatcher.PublierAsync(new NotificationTempsReel(
                creee.UtilisateurId, creee.Id, creee.Type, creee.Titre,
                creee.Message, creee.Lien, creee.DateCreation));
        }

        if (canaux.Email)
            await MettreEnFileEmailAsync(type, titre, message, lien, new[] { destinataireId }, acteurId);

        return creee;
    }

    public async Task NotifierGroupeAsync(
        TypeNotification type, string message, IEnumerable<Ulid> destinatairesIds,
        string? titre = null, string? lien = null, string? cleEntite = null, Ulid? acteurId = null)
    {
        var ids = destinatairesIds.Where(id => id != Ulid.Empty).Distinct().ToList();
        if (ids.Count == 0) return;

        var canaux = await _parametres.ObtenirCanauxAsync(type);

        if (canaux.Interne)
        {
            var lot = ids.Select(id => new Notification
            {
                UtilisateurId = id,
                Type          = type,
                Titre         = titre,
                Message       = message,
                Lien          = lien,
                CleEntite     = cleEntite,
                ActeurId      = acteurId,
                EstLue        = false,
                DateCreation  = DateTime.UtcNow
            });

            var creees = await _store.AjouterLotAsync(lot);

            foreach (var n in creees)
                await _dispatcher.PublierAsync(new NotificationTempsReel(
                    n.UtilisateurId, n.Id, n.Type, n.Titre, n.Message, n.Lien, n.DateCreation));
        }

        if (canaux.Email)
            await MettreEnFileEmailAsync(type, titre, message, lien, ids, acteurId);
    }

    // ── Lecture ──────────────────────────────────────────────────────────────

    public Task<int> CompterNonLuesAsync(Ulid utilisateurId)
        => _store.CompterNonLuesAsync(utilisateurId);

    public Task<List<Notification>> ObtenirRecentesAsync(Ulid utilisateurId, int max = 8, bool uniquementNonLues = false)
        => _store.ObtenirRecentesAsync(utilisateurId, max, uniquementNonLues);

    public Task<(List<Notification> Items, int Total, int NonLues)> ObtenirPageAsync(
        Ulid utilisateurId, int page = 1, int taille = 20,
        bool? estLue = null, TypeNotification? type = null, string? recherche = null)
        => _store.ObtenirPageAsync(utilisateurId, page, taille, estLue, type, recherche);

    public Task MarquerLueAsync(Ulid id, Ulid utilisateurId) => _store.MarquerLueAsync(id, utilisateurId);
    public Task MarquerNonLueAsync(Ulid id, Ulid utilisateurId) => _store.MarquerNonLueAsync(id, utilisateurId);
    public Task MarquerToutesLuesAsync(Ulid utilisateurId) => _store.MarquerToutesLuesAsync(utilisateurId);
    public Task<int> SupprimerAsync(Ulid id, Ulid utilisateurId) => _store.SupprimerAsync(id, utilisateurId);
    public Task<int> SupprimerLuesAsync(Ulid utilisateurId) => _store.SupprimerLuesAsync(utilisateurId);

    // ── Email ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Résout les adresses des destinataires puis met un email en file. L'envoi lui-même
    /// est asynchrone (EmailQueueProcessor) : un relais SMTP lent ne ralentit jamais
    /// l'action métier de l'utilisateur.
    /// </summary>
    private async Task MettreEnFileEmailAsync(
        TypeNotification type, string? titre, string message, string? lien,
        IReadOnlyCollection<Ulid> destinatairesIds, Ulid? acteurId)
    {
        if (!_emailOptions.Active) return;

        // On ne s'envoie pas d'email pour ses propres actions.
        var cibles = destinatairesIds.Where(id => id != acteurId).Distinct().ToList();
        if (cibles.Count == 0) return;

        var adresses = new List<string>();
        foreach (var id in cibles)
        {
            var u = await _utilisateurs.TrouverParIdAsync(id);
            if (u is { EstActif: true } && !string.IsNullOrWhiteSpace(u.Email))
                adresses.Add(u.Email);
        }
        if (adresses.Count == 0) return;

        string? nomActeur = null, emailActeur = null;
        if (acteurId is { } idActeur && idActeur != Ulid.Empty)
        {
            var acteur = await _utilisateurs.TrouverParIdAsync(idActeur);
            nomActeur = acteur?.NomPrenoms;
            emailActeur = string.IsNullOrWhiteSpace(acteur?.Email) ? null : acteur!.Email;
        }

        _emails.Enqueue(new EmailMessage
        {
            Destinataires        = adresses,
            Sujet                = NotificationEmailFormatter.Sujet(_application.Nom, type, titre),
            CorpsHtml            = NotificationEmailFormatter.CorpsHtml(_application, type, titre, message, lien, _emailOptions.BaseUrl),
            NomExpediteurAffiche = nomActeur,
            ReplyTo              = emailActeur
        });
    }
}


