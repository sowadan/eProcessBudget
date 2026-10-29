namespace eProcessBudget.Web.Features.Axes.Commun;

/// <summary>Petits utilitaires d'affichage des écrans des axes.</summary>
public static class RequetesAffichage
{
    /// <summary>Éléments non vides séparés par « · ».</summary>
    public static string Joindre(params string?[] elements) => string.Join(" · ", elements.Where(e => !string.IsNullOrEmpty(e)));
}
