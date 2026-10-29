using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace eProcessBudget.Web.Securite;

/// <summary>
/// Points de terminaison HTTP de la session. Ils ne peuvent pas être des composants
/// interactifs : poser ou retirer un cookie exige une vraie requête HTTP, pas un message
/// sur le circuit SignalR.
/// </summary>
public static class PointsTerminaisonCompte
{
    public const string MotifInactivite = "inactivite";

    public static IEndpointRouteBuilder MapPointsTerminaisonCompte(this IEndpointRouteBuilder app)
    {
        // Déconnexion, volontaire ou sur inactivité (formulaire soumis par wwwroot/js/session.js).
        // Anonyme admis : une session déjà expirée doit pouvoir « se déconnecter » sans erreur.
        // Pas de jeton antifalsification : il serait invalide une fois la session expirée, et le
        // cookie SameSite=Strict n'est jamais envoyé par un formulaire d'un autre site.
        app.MapPost(ConfigurationSecurite.CheminDeconnexion, async (
                HttpContext http, IAuthentificationService authentification, [FromForm] string? motif) =>
            {
                var inactivite = motif == MotifInactivite;

                if (http.User.Identity?.IsAuthenticated == true)
                {
                    await authentification.JournaliserDeconnexionAsync(http.User,
                        http.Connection.RemoteIpAddress?.ToString() ?? "inconnue",
                        http.Request.Headers.UserAgent.ToString(),
                        inactivite ? "Expiration après inactivité" : "Déconnexion volontaire");
                }

                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.LocalRedirect(
                    $"{ConfigurationSecurite.CheminConnexion}?motif={(inactivite ? MotifInactivite : "deconnexion")}");
            })
            .AllowAnonymous()
            .DisableAntiforgery();

        // Signal d'activité envoyé par le navigateur pendant que l'utilisateur travaille :
        // le middleware de cookie renouvelle alors l'expiration glissante.
        app.MapGet("/compte/session", (HttpContext http) =>
            {
                http.Response.Headers.CacheControl = "no-store";
                return Results.NoContent();
            });

        return app;
    }
}
