using eProcessBudget.Core.Enums;

namespace eProcessBudget.Web.Features.Axes.Commun;

/// <summary>Routes des écrans des axes, pour les liens entre pages.</summary>
public static class RoutesAxes
{
    public const string Entretien = "/axes/entretien";
    public const string Investissements = "/axes/investissements";
    public const string Fonctionnement = "/axes/fonctionnement";
    public const string Appuis = "/axes/appuis";

    /// <summary>Liste des projets de l'axe (sa page d'entrée).</summary>
    public static string Projets(AxeDepense axe) => axe.Code switch
    {
        "ENTRETIEN" => Entretien,
        "INVESTISSEMENT" => Investissements,
        "APPUIS" => Appuis,
        _ => Fonctionnement,
    };
}
