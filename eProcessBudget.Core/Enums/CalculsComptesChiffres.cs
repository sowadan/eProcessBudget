namespace eProcessBudget.Core.Enums;

/// <summary>
/// Une ligne du compte sur chiffres des recettes (réponse du métier C.2.9) : prévision
/// ajustée, factures émises, factures recouvrées (encaissé).
/// </summary>
public sealed record ChiffresRecette(decimal Prevision, decimal Emis, decimal Recouvre)
{
    public static readonly ChiffresRecette Zero = new(0, 0, 0);

    /// <summary>% d'exécution : émis / prévision ajustée.</summary>
    public decimal? TauxExecution => CalculsExecution.Taux(Emis, Prevision);

    /// <summary>% de recouvrement : recouvré / émis.</summary>
    public decimal? TauxRecouvrement => CalculsExecution.Taux(Recouvre, Emis);

    /// <summary>Reste à recouvrer : émis − recouvré.</summary>
    public decimal ResteARecouvrer => Emis - Recouvre;

    /// <summary>% du reste à recouvrer : reste / émis.</summary>
    public decimal? TauxResteARecouvrer => CalculsExecution.Taux(ResteARecouvrer, Emis);

    public static ChiffresRecette operator +(ChiffresRecette a, ChiffresRecette b) => new(a.Prevision + b.Prevision, a.Emis + b.Emis, a.Recouvre + b.Recouvre);

    public static ChiffresRecette Somme(IEnumerable<ChiffresRecette> lignes) => lignes.Aggregate(Zero, (a, b) => a + b);
}

/// <summary>
/// Une ligne du compte sur chiffres des dépenses (réponses C.2.2 et C.2.9) : prévision
/// ajustée, prise en charge (factures reçues), rejets ou pénalités, paiements.
/// </summary>
public sealed record ChiffresDepense(decimal Prevision, decimal PriseEnCharge, decimal Rejets, decimal Paiements)
{
    public static readonly ChiffresDepense Zero = new(0, 0, 0, 0);

    /// <summary>% d'exécution : prise en charge / prévision ajustée.</summary>
    public decimal? TauxExecution => CalculsExecution.Taux(PriseEnCharge, Prevision);

    /// <summary>% de paiement : paiements / prise en charge.</summary>
    public decimal? TauxPaiement => CalculsExecution.Taux(Paiements, PriseEnCharge);

    /// <summary>Reste à payer : prise en charge − paiements − rejets ou pénalités.</summary>
    public decimal ResteAPayer => PriseEnCharge - Paiements - Rejets;

    /// <summary>% du reste à payer : reste / prise en charge.</summary>
    public decimal? TauxResteAPayer => CalculsExecution.Taux(ResteAPayer, PriseEnCharge);

    public static ChiffresDepense operator +(ChiffresDepense a, ChiffresDepense b)
        => new(a.Prevision + b.Prevision, a.PriseEnCharge + b.PriseEnCharge, a.Rejets + b.Rejets, a.Paiements + b.Paiements);

    public static ChiffresDepense Somme(IEnumerable<ChiffresDepense> lignes) => lignes.Aggregate(Zero, (a, b) => a + b);
}

/// <summary>
/// Équilibre de trésorerie de fin d'exercice (réponses C.2.7 et C.2.9), base des reports de
/// l'exercice suivant. Le contrôle H = I tient par construction : H = émis − pris en charge.
/// </summary>
/// <param name="RecettesRecouvrees">(A) total général des recettes recouvrées, reports de ressources compris.</param>
/// <param name="EngagementsPayes">(B) paiements, rejets et pénalités compris.</param>
/// <param name="EncoursAnterieurs">(D) reste à payer sur les factures prises en charge : encours reporté sur l'exercice suivant.</param>
/// <param name="TaxesNonRecouvrees">(F) reste à recouvrer des taxes affectées.</param>
/// <param name="RecettesPropresNonRecouvrees">(G) reste à recouvrer des autres recettes (péage, pesage...).</param>
/// <param name="RecettesEmises">Total général des recettes émises (I, premier terme).</param>
/// <param name="DepensesPrisesEnCharge">Total général des dépenses prises en charge (I, second terme).</param>
public sealed record EquilibreTresorerie(
    decimal RecettesRecouvrees, decimal EngagementsPayes, decimal EncoursAnterieurs,
    decimal TaxesNonRecouvrees, decimal RecettesPropresNonRecouvrees, decimal RecettesEmises, decimal DepensesPrisesEnCharge)
{
    /// <summary>(C) trésorerie sans déduction des encours : A − B.</summary>
    public decimal TresorerieSansEncours => RecettesRecouvrees - EngagementsPayes;

    /// <summary>(E) trésorerie disponible en début d'année : C − D.</summary>
    public decimal TresorerieDisponible => TresorerieSansEncours - EncoursAnterieurs;

    /// <summary>(H) total des reports de ressources : E + F + G.</summary>
    public decimal ReportsRessources => TresorerieDisponible + TaxesNonRecouvrees + RecettesPropresNonRecouvrees;

    /// <summary>(I) recettes collectées (émises) − dépenses engagées (prises en charge).</summary>
    public decimal SoldeEmisPrisEnCharge => RecettesEmises - DepensesPrisesEnCharge;

    /// <summary>Écart H − I : nul quand les données sont cohérentes.</summary>
    public decimal Ecart => ReportsRessources - SoldeEmisPrisEnCharge;
}

/// <summary>Règles de l'état de présentation du budget (C.2.9).</summary>
public static class CalculsPresentation
{
    /// <summary>Seuil d'une variation significative entre deux exercices, en % (commentaire attendu).</summary>
    public const decimal SeuilVariationSignificative = 10m;

    /// <summary>Variation relative (N − N-1) / N-1, en % ; null sans montant N-1.</summary>
    public static decimal? TauxVariation(decimal n, decimal n1) => CalculsExecution.Taux(n - n1, n1);

    /// <summary>Variation significative : au-delà du seuil, ou apparition d'un montant sans antériorité.</summary>
    public static bool EstSignificative(decimal n, decimal n1)
        => TauxVariation(n, n1) is { } t ? Math.Abs(t) >= SeuilVariationSignificative : n != 0;
}
