using eProcessBudget.Infrastructure.Services.Rapports;
using eProcessBudget.Infrastructure.Services.Securite;

namespace eProcessBudget.Web.Features.Rapports;

/// <summary>
/// Points de terminaison HTTP des rapports : ouverture d'un fichier produit dans un onglet du
/// navigateur, pour l'impression directe (cahier fonctionnel § 3.12.3).
/// </summary>
public static class PointsTerminaisonRapports
{
    public static IEndpointRouteBuilder MapPointsTerminaisonRapports(this IEndpointRouteBuilder app)
    {
        // Fichier déposé par l'écran Rapports : réservé à l'utilisateur qui l'a produit (session exigée).
        app.MapGet("/rapports/fichier/{jeton}", async (string jeton, bool? telecharger, DepotRapports depot, IUserContextService utilisateur) =>
        {
            var fichier = depot.Obtenir(jeton, await utilisateur.ObtenirUserIdAsync());
            if (fichier is null) return Results.NotFound();
            return telecharger == true
                ? Results.File(fichier.Contenu, fichier.ContentType, fichier.NomFichier)
                : Results.File(fichier.Contenu, fichier.ContentType, enableRangeProcessing: false);
        });

        return app;
    }
}
