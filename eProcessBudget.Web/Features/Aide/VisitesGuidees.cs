using Microsoft.AspNetCore.WebUtilities;
using Microsoft.JSInterop;

namespace eProcessBudget.Web.Features.Aide;

/// <summary>
/// Lancement des visites guidées depuis C# : choisit le guide, le transmet à
/// wwwroot/js/guide.js (window.epbGuide), qui le joue avec driver.js.
/// </summary>
public static class VisitesGuidees
{
    /// <summary>Visite de l'écran à cette adresse ; visite générique s'il n'a pas de guide.</summary>
    public static ValueTask EcranAsync(IJSRuntime js, string chemin)
        => AppelerAsync(js, "epbGuide.lancer", GuidesApplication.Pour(chemin)?.PourNavigateur() ?? GuidesApplication.VisiteGenerique());

    /// <summary>Visite de l'interface : menu, en-tête, notifications, aide, compte.</summary>
    public static ValueTask InterfaceAsync(IJSRuntime js, string nomApplication)
        => AppelerAsync(js, "epbGuide.lancer", GuidesApplication.VisiteInterface(nomApplication));

    /// <summary>Visite de l'interface, une seule fois par utilisateur et par navigateur.</summary>
    public static ValueTask PremiereVisiteAsync(IJSRuntime js, Ulid userId, string nomApplication)
        => AppelerAsync(js, "epbGuide.premiereVisite", $"{GuidesApplication.CleInterface}.{userId}",
                        GuidesApplication.VisiteInterface(nomApplication));

    /// <summary>
    /// Joue la visite demandée par l'adresse (?visite=page ou ?visite=interface), posée par
    /// le guide d'utilisation pour ouvrir un écran et le présenter aussitôt.
    /// </summary>
    /// <returns>true si une visite était demandée.</returns>
    public static async Task<bool> DemandeeAsync(IJSRuntime js, string cheminEtRequete, string nomApplication)
    {
        var position = cheminEtRequete.IndexOf('?');
        if (position < 0) return false;

        var requete = QueryHelpers.ParseQuery(cheminEtRequete[position..]);
        if (!requete.TryGetValue("visite", out var visite)) return false;

        if (visite == GuidesApplication.CleInterface) await InterfaceAsync(js, nomApplication);
        else await EcranAsync(js, cheminEtRequete[..position]);
        return true;
    }

    /// <summary>Interrompt la visite en cours (changement d'écran).</summary>
    public static ValueTask ArreterAsync(IJSRuntime js) => AppelerAsync(js, "epbGuide.arreter");

    /// <summary>Adresse qui ouvre l'écran d'un guide et y lance sa visite.</summary>
    public static string Lien(GuideUtilisation guide) => $"{guide.Route}?visite={GuidesApplication.ClePage}";

    private static async ValueTask AppelerAsync(IJSRuntime js, string fonction, params object?[] arguments)
    {
        try
        {
            await js.InvokeVoidAsync(fonction, arguments);
        }
        catch (JSDisconnectedException)
        {
            // Onglet fermé : plus personne à guider.
        }
    }
}
