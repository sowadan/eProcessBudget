namespace eProcessBudget.Core.Enums;

/// <summary>
/// Montants d'une prévision de recette (ou d'un regroupement de prévisions), tels que lus
/// en base, et les grandeurs qui en découlent (cahier fonctionnel § 4.1). Seuls les
/// encaissements validés y entrent ; la réalisation d'une recette est son encaissement.
///
/// Mêmes formules que la vue rec.vw_SituationRecette.
/// </summary>
public sealed record MontantsRecette(decimal PrevisionInitiale, decimal PrevisionRetenue, decimal Encaisse, decimal EncaissePositionne, decimal Emis = 0)
{
    public static readonly MontantsRecette Zero = new(0, 0, 0, 0);

    // ── Compte sur chiffres (réponse C.2.9) : factures émises puis recouvrées ──

    /// <summary>% d'exécution : factures émises / prévision ajustée.</summary>
    public decimal? TauxExecution => CalculsExecution.Taux(Emis, PrevisionRetenue);

    /// <summary>% de recouvrement des factures : recouvré (encaissé) / émis.</summary>
    public decimal? TauxRecouvrementEmis => CalculsExecution.Taux(Encaisse, Emis);

    /// <summary>Reste à recouvrer sur les factures émises : émis − recouvré.</summary>
    public decimal ResteARecouvrerEmis => Emis - Encaisse;

    /// <summary>% du reste à recouvrer : reste / émis.</summary>
    public decimal? TauxResteARecouvrer => CalculsExecution.Taux(ResteARecouvrerEmis, Emis);

    /// <summary>Révision : prévision retenue − prévision initiale.</summary>
    public decimal Revision => PrevisionRetenue - PrevisionInitiale;

    /// <summary>Reste à recouvrer : prévision retenue − encaissé (négatif au-delà de la prévision).</summary>
    public decimal Ecart => PrevisionRetenue - Encaisse;

    /// <summary>Encaissé mais pas positionné sur un compte bancaire.</summary>
    public decimal EncaisseNonPositionne => Encaisse - EncaissePositionne;

    /// <summary>Taux de recouvrement : encaissé / prévision retenue ; « n.s. » si la prévision est nulle.</summary>
    public decimal? TauxRecouvrement => CalculsExecution.Taux(Encaisse, PrevisionRetenue);

    public static MontantsRecette operator +(MontantsRecette a, MontantsRecette b) => new(
        a.PrevisionInitiale + b.PrevisionInitiale, a.PrevisionRetenue + b.PrevisionRetenue,
        a.Encaisse + b.Encaisse, a.EncaissePositionne + b.EncaissePositionne, a.Emis + b.Emis);

    public static MontantsRecette Somme(IEnumerable<MontantsRecette> montants) => montants.Aggregate(Zero, (a, b) => a + b);
}

/// <summary>Formules et règles des recettes (cahier fonctionnel § 3.9 et 4.1).</summary>
public static class CalculsRecettes
{
    /// <summary>Prévision retenue : la révisée si elle existe, sinon l'initiale.</summary>
    public static decimal PrevisionRetenue(decimal initiale, decimal? revisee) => revisee ?? initiale;

    /// <summary>Taux d'accroissement des ressources : (N − N-1) / N-1 ; « n.s. » sans ressources N-1.</summary>
    public static decimal? TauxAccroissement(decimal ressourcesN, decimal ressourcesN1)
        => CalculsExecution.Taux(ressourcesN - ressourcesN1, ressourcesN1);

    /// <summary>Bilan recettes / dépenses : total des recettes réalisées − total des dépenses réalisées.</summary>
    public static decimal Bilan(decimal recettes, decimal depenses) => recettes - depenses;

    /// <summary>
    /// Situation de chaque échéance d'une prévision : les encaissements couvrent les
    /// échéances dans l'ordre chronologique. Une échéance est en retard si sa date est
    /// passée et que l'encaissé ne couvre pas le cumul attendu à cette date ; son retard est
    /// la part non couverte de son propre montant.
    /// </summary>
    public static IReadOnlyList<(DateOnly Date, decimal Attendu, decimal Couvert, bool EnRetard)> Couverture(
        IEnumerable<(DateOnly Date, decimal Attendu)> echeances, decimal encaisse, DateOnly aujourdhui)
    {
        var reste = Math.Max(0, encaisse);
        var resultat = new List<(DateOnly, decimal, decimal, bool)>();
        foreach (var (date, attendu) in echeances.OrderBy(e => e.Date))
        {
            var couvert = Math.Min(attendu, reste);
            reste -= couvert;
            resultat.Add((date, attendu, couvert, date < aujourdhui && couvert < attendu));
        }
        return resultat;
    }
}

/// <summary>Libellés propres aux recettes, partagés par les écrans.</summary>
public static class LibellesRecettes
{
    /// <summary>Statut d'une échéance de recouvrement.</summary>
    public static string StatutEcheance(decimal attendu, decimal couvert, bool enRetard)
        => couvert >= attendu ? "Couverte" : enRetard ? "En retard" : couvert > 0 ? "Partiellement couverte" : "À venir";
}
