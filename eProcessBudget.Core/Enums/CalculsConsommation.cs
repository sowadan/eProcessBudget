namespace eProcessBudget.Core.Enums;

/// <summary>Formules de la consommation budgétaire (cahier fonctionnel § 3.10, technique § 4.7).</summary>
public static class CalculsConsommation
{
    /// <summary>
    /// Part écoulée de l'exercice à une date, entre 0 et 1 (jours écoulés, jour courant
    /// compris / jours de l'exercice). 1 au-delà de la fin, 0 avant le début.
    /// </summary>
    public static decimal PartEcoulee(DateOnly debut, DateOnly fin, DateOnly date)
    {
        if (date < debut) return 0;
        if (date >= fin) return 1;
        var total = fin.DayNumber - debut.DayNumber + 1;
        return (decimal)(date.DayNumber - debut.DayNumber + 1) / total;
    }

    /// <summary>
    /// Projection de fin d'exercice par extrapolation linéaire : réalisé à date / part écoulée,
    /// arrondie au centime. Le réalisé lui-même une fois l'exercice écoulé ; null avant son début.
    /// </summary>
    public static decimal? Projection(decimal realise, decimal partEcoulee)
        => partEcoulee <= 0 ? null : Math.Round(realise / partEcoulee, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Une imputation dérape quand ses engagements dépassent sa dotation totale, ou que la
    /// projection de ses paiements la dépasse.
    /// </summary>
    public static bool Derape(MontantsDotation m, decimal? paiementsProjetes)
        => m.Disponible < 0 || (paiementsProjetes is { } p && m.DotationTotale > 0 && p > m.DotationTotale);
}

/// <summary>Libellés propres à la consommation et au tableau de bord.</summary>
public static class LibellesConsommation
{
    /// <summary>Libellé court d'une période : « janv. 2027 », « T1 2027 », « S2 2027 », « 2027 ».</summary>
    public static string Periode(TypePeriode type, int numero, DateOnly debut) => type switch
    {
        TypePeriode.Mois => debut.ToString("MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")),
        TypePeriode.Trimestre => $"T{numero} {debut.Year}",
        TypePeriode.Semestre => $"S{numero} {debut.Year}",
        _ => debut.Year.ToString(),
    };

    /// <summary>Volets du budget global affichés au tableau de bord (§ 3.11.2), par code d'axe.</summary>
    public static readonly IReadOnlyDictionary<string, string> Volets = new Dictionary<string, string>
    {
        ["ENTRETIEN"] = "Travaux",
        ["INVESTISSEMENT"] = "Investissement",
        ["FONCTIONNEMENT"] = "Fonctionnement",
        ["APPUIS"] = "Appuis",
    };
}
