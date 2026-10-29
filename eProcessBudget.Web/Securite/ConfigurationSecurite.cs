using System.Threading.RateLimiting;
using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace eProcessBudget.Web.Securite;

/// <summary>
/// Sécurité de l'hôte web (cahier technique § 2.6) : authentification par cookie, accès
/// authentifié par défaut, limitation de débit des pages sensibles et en-têtes HTTP.
///
/// Cookie plutôt que jeton (JWT) : le circuit SignalR de Blazor Server s'ouvre avec les
/// cookies du navigateur, sans code client pour transmettre un jeton.
/// </summary>
public static class ConfigurationSecurite
{
    public const string CheminConnexion = "/compte/connexion";
    public const string CheminDeconnexion = "/compte/deconnexion";
    public const string ParametreRetour = "retour";

    private const string NomCookie = "epb.session";

    /// <summary>POST soumis à la limitation de débit : on ne devine pas un mot de passe par force brute.</summary>
    private static readonly string[] CheminsLimites =
        [CheminConnexion, "/compte/mot-de-passe-oublie", "/compte/reinitialiser"];

    public static IServiceCollection AddSecuriteWeb(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environnement)
    {
        var securite = configuration.GetSection(SecuriteOptions.SectionName).Get<SecuriteOptions>() ?? new SecuriteOptions();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = NomCookie;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                // Secure obligatoire hors développement (HTTPS) ; en local, l'application
                // peut tourner en HTTP sur localhost.
                o.Cookie.SecurePolicy = environnement.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                // Valeur par défaut ; chaque connexion fixe sa propre durée (paramètre de
                // l'organisme), conservée à chaque renouvellement glissant.
                o.ExpireTimeSpan = TimeSpan.FromMinutes(Math.Max(5, securite.DureeSessionMinutes));
                o.SlidingExpiration = true;

                o.LoginPath = CheminConnexion;
                o.LogoutPath = CheminDeconnexion;
                o.AccessDeniedPath = "/";
                o.ReturnUrlParameter = ParametreRetour;

                // À chaque requête HTTP : compte désactivé, bloqué, mot de passe changé ou
                // organisme suspendu ferment la session (résultat mis en cache quelques secondes).
                o.Events.OnValidatePrincipal = async contexte =>
                {
                    var authentification = contexte.HttpContext.RequestServices.GetRequiredService<IAuthentificationService>();
                    if (contexte.Principal is null || !await authentification.ValiderSessionAsync(contexte.Principal))
                    {
                        contexte.RejectPrincipal();
                        await contexte.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            });

        // Sûr par défaut : toute page exige une session, sauf celles marquées [AllowAnonymous].
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        // État d'authentification des composants, revérifié périodiquement pendant la vie du circuit.
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, EtatAuthentificationRevalide>();

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (!HttpMethods.IsPost(http.Request.Method)
                    || !CheminsLimites.Any(c => http.Request.Path.Equals(c, StringComparison.OrdinalIgnoreCase)))
                    return RateLimitPartition.GetNoLimiter(string.Empty);

                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "inconnue";
                return RateLimitPartition.GetFixedWindowLimiter($"compte:{ip}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, securite.LimiteTentativesParMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
            o.OnRejected = async (contexte, jeton) =>
            {
                contexte.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await contexte.HttpContext.Response.WriteAsync(
                    "Trop de tentatives depuis votre poste. Patientez une minute avant de réessayer.", jeton);
            };
        });

        return services;
    }

    /// <summary>Politique frame-ancestors que Blazor ajoute aux pages interactives : alignée sur la CSP.</summary>
    public const string FrameAncestors = "'none'";

    /// <summary>
    /// En-têtes de sécurité sur toutes les réponses. Tous les scripts sont servis par
    /// l'application (Tailwind est une feuille figée, docs/07-composants-ui.md) ; script-src
    /// garde 'unsafe-inline' pour la seule table d'import en ligne de &lt;ImportMap /&gt;
    /// (App.razor), régénérée à chaque déploiement.
    /// </summary>
    public static IApplicationBuilder UseEnTetesSecurite(this IApplicationBuilder app)
        => app.Use(async (http, suite) =>
        {
            var entetes = http.Response.Headers;
            entetes["X-Content-Type-Options"] = "nosniff";
            entetes["X-Frame-Options"] = "DENY";
            entetes["Referrer-Policy"] = "same-origin";
            entetes["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

            // Pas de Cache-Control ici : toute page qui porte un formulaire (connexion, mots de
            // passe) reçoit déjà « no-store » de la protection antifalsification.
            await suite();
        });
}
