using eProcessBudget.Infrastructure.Services.Parametrage;

namespace eProcessBudget.Web.Features.Parametrage;

/// <summary>
/// Points de terminaison HTTP du Paramétrage : ce qu'une balise &lt;img&gt; doit pouvoir
/// charger directement, hors circuit Blazor.
/// </summary>
public static class PointsTerminaisonParametrage
{
    public static IEndpointRouteBuilder MapPointsTerminaisonParametrage(this IEndpointRouteBuilder app)
    {
        // Logo de l'organisme de l'utilisateur connecté (session exigée : politique par défaut).
        // Le type vient de la signature vérifiée à l'envoi (PNG ou JPEG uniquement).
        app.MapGet("/parametrage/logo", async (HttpContext http, IParametresOrganismeService parametres) =>
        {
            var logo = await parametres.ObtenirLogoAsync();
            if (logo is null) return Results.NotFound();

            http.Response.Headers.CacheControl = "private, no-cache";
            return Results.File(logo.Value.Contenu, logo.Value.ContentType);
        });

        return app;
    }
}
