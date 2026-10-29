using System.Net;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Options;

namespace eProcessBudget.Infrastructure.Services.Notifications;

/// <summary>
/// Construit le sujet et le corps HTML des emails de notification.
///
/// Le gabarit reprend l'identité définie dans la section Application (nom et couleurs) :
/// renommer l'application ou changer sa couleur primaire met à jour les emails sans
/// toucher à ce fichier.
///
/// Contraintes de l'email HTML respectées ici : tableaux plutôt que flexbox, styles en
/// ligne plutôt que feuille de style, largeur fixe de 600 px - c'est ce que les clients
/// de messagerie d'entreprise savent rendre correctement.
/// </summary>
public static class NotificationEmailFormatter
{
    public static string Libelle(TypeNotification type) => type switch
    {
        TypeNotification.Information   => "Information",
        TypeNotification.Succes        => "Opération réussie",
        TypeNotification.Avertissement => "Avertissement",
        TypeNotification.Erreur        => "Erreur",
        TypeNotification.ActionRequise => "Action requise",
        TypeNotification.Rappel        => "Rappel",
        _ => type.ToString()
    };

    public static string Sujet(string nomApplication, TypeNotification type, string? titre)
        => $"[{nomApplication}] {(string.IsNullOrWhiteSpace(titre) ? Libelle(type) : titre)}";

    public static string CorpsHtml(
        ApplicationOptions app, TypeNotification type,
        string? titre, string message, string? lien, string baseUrl)
    {
        var titreAffiche = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(titre) ? Libelle(type) : titre);
        var corps        = WebUtility.HtmlEncode(message);
        var nomApp       = WebUtility.HtmlEncode(app.Nom);
        var description  = WebUtility.HtmlEncode(app.Description);
        var primaire     = app.Couleurs.Primaire;
        var secondaire   = app.Couleurs.Secondaire;

        var bouton = "";
        var url = ConstruireUrlAbsolue(lien, baseUrl);
        if (!string.IsNullOrWhiteSpace(url))
        {
            var urlEnc = WebUtility.HtmlEncode(url);
            bouton = $@"
                <tr><td style=""padding:20px 0 0 0;"">
                    <a href=""{urlEnc}"" style=""display:inline-block;background:{primaire};color:#ffffff;
                       text-decoration:none;padding:11px 22px;border-radius:6px;font-weight:600;font-size:14px;"">
                        Ouvrir dans {nomApp}
                    </a>
                </td></tr>";
        }

        return $@"<!DOCTYPE html>
<html lang=""fr""><head><meta charset=""utf-8""></head>
<body style=""margin:0;padding:0;background:#f3f4f6;font-family:Segoe UI,Arial,sans-serif;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f3f4f6;padding:24px 0;"">
    <tr><td align=""center"">
      <table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0""
             style=""background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e5e7eb;"">
        <tr><td style=""background:{secondaire};padding:18px 28px;"">
            <span style=""color:#ffffff;font-size:16px;font-weight:700;letter-spacing:.5px;"">{nomApp}</span>
            <span style=""color:rgba(255,255,255,.65);font-size:12px;""> &middot; {description}</span>
        </td></tr>
        <tr><td style=""padding:28px;"">
          <h1 style=""margin:0 0 12px 0;font-size:18px;color:{secondaire};"">{titreAffiche}</h1>
          <p style=""margin:0;font-size:14px;line-height:1.6;color:#374151;"">{corps}</p>
          <table role=""presentation"" cellpadding=""0"" cellspacing=""0"">{bouton}</table>
        </td></tr>
        <tr><td style=""padding:16px 28px;background:#f9fafb;border-top:1px solid #e5e7eb;"">
          <p style=""margin:0;font-size:11px;color:#9ca3af;"">
            Email automatique - merci de ne pas répondre à cette adresse.
            Rendez-vous sur {nomApp} pour gérer vos notifications.
          </p>
        </td></tr>
      </table>
    </td></tr>
  </table>
</body></html>";
    }

    /// <summary>
    /// Un lien de notification est relatif ("/commandes/18"). Sans BaseUrl configurée,
    /// on n'affiche pas de bouton plutôt que d'envoyer un lien mort.
    /// </summary>
    private static string? ConstruireUrlAbsolue(string? lien, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(lien)) return null;

        if (lien.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            lien.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return lien;

        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        return $"{baseUrl.TrimEnd('/')}/{lien.TrimStart('/')}";
    }
}


