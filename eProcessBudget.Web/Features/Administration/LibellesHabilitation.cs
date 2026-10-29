using eProcessBudget.Core.Enums;

namespace eProcessBudget.Web.Features.Administration;

/// <summary>Libellés et couleurs d'affichage des habilitations (énumérations du § 3.6).</summary>
public static class LibellesHabilitation
{
    public static string Perimetre(TypePerimetre t) => t switch
    {
        TypePerimetre.OrganismeEntier => "Tout l'organisme",
        TypePerimetre.Axe             => "Axe",
        TypePerimetre.Programme       => "Programme",
        TypePerimetre.Action          => "Action",
        TypePerimetre.Site            => "Site",
        TypePerimetre.Structure       => "Structure",
        _ => t.ToString()
    };

    public static string Statut(StatutHabilitation s) => s switch
    {
        StatutHabilitation.Demandee         => "Demandée",
        StatutHabilitation.ValideeFinancier => "Validée (financier)",
        StatutHabilitation.ValideeAdminSI   => "Validée (admin. SI)",
        StatutHabilitation.Active           => "Active",
        StatutHabilitation.Rejetee          => "Rejetée",
        StatutHabilitation.Revoquee         => "Révoquée",
        _ => s.ToString()
    };

    /// <summary>Variante d'AppBadge.</summary>
    public static string Variante(StatutHabilitation s) => s switch
    {
        StatutHabilitation.Active   => "success",
        StatutHabilitation.Rejetee  => "danger",
        StatutHabilitation.Revoquee => "neutral",
        _                           => "warning"
    };

    public static bool EstEnAttente(StatutHabilitation s)
        => s is StatutHabilitation.Demandee or StatutHabilitation.ValideeFinancier or StatutHabilitation.ValideeAdminSI;
}
