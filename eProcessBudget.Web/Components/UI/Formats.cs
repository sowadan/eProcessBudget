using System.Globalization;

namespace eProcessBudget.Web.Components.UI;

/// <summary>
/// Formats d'affichage des nombres, en français quelle que soit la culture du serveur :
/// montants à 2 décimales (§ 4.3), taux « n.s. » quand le dénominateur est nul (§ 4.1).
/// </summary>
public static class Formats
{
    public static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    public static string Montant(decimal? m) => m is null ? "—" : m.Value.ToString("N2", Francais);
    public static string Nombre(decimal? n) => n is null ? "—" : n.Value.ToString("#,##0.####", Francais);
    public static string Taux(decimal? t) => t is null ? "n.s." : t.Value.ToString("N2", Francais) + " %";
    public static string Pourcentage(decimal p) => p.ToString("0.####", Francais) + " %";
}
