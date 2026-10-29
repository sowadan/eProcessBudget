namespace eProcessBudget.Core.Enums;

/// <summary>
/// Montants d'une dotation (ou d'un regroupement de dotations), tels que lus en base, et
/// les grandeurs qui en découlent (cahier fonctionnel § 4.1). Seules les pièces validées y
/// entrent ; la réalisation se mesure par les paiements (C.2.2).
///
/// Ces formules sont celles de la vue bud.vw_SituationDotation : la vue sert les états et
/// le tableau de bord, ce type sert les écrans de saisie et les contrôles.
/// </summary>
public sealed record MontantsDotation(
    decimal MontantInitial, decimal Ajustements, decimal Reports,
    decimal AEPeriode, decimal AEReports,
    decimal PaiementsPeriode, decimal PaiementsTotal, decimal RejetsPenalites)
{
    public static readonly MontantsDotation Zero = new(0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Prévision initiale ± ajustements.</summary>
    public decimal PrevisionAjustee => MontantInitial + Ajustements;

    /// <summary>Prévision ajustée + reports (dotation totale).</summary>
    public decimal DotationTotale => PrevisionAjustee + Reports;

    /// <summary>AE / AD de l'exercice et encours reportés.</summary>
    public decimal AETotal => AEPeriode + AEReports;

    /// <summary>Crédits non encore engagés (négatif en cas de dépassement).</summary>
    public decimal Disponible => DotationTotale - AETotal;

    /// <summary>AD de l'exercice − paiements sur ces AD.</summary>
    public decimal ResteAPayerPeriode => AEPeriode - PaiementsPeriode;

    /// <summary>AD y compris reports − paiements y compris reports.</summary>
    public decimal ResteAPayerYCReports => AETotal - PaiementsTotal;

    /// <summary>AD y compris antérieures − paiements − rejets ou pénalités (C.2.6).</summary>
    public decimal EncoursAReporter => AETotal - PaiementsTotal - RejetsPenalites;

    /// <summary>Écart budgétaire / reliquat : dotation totale − réalisé.</summary>
    public decimal Ecart => DotationTotale - PaiementsTotal;

    /// <summary>
    /// Crédits à reporter en reliquat à la clôture : ce qui n'est ni payé ni en encours.
    /// Jamais négatif (le report d'un dépassement, C.2.5, est désactivé).
    /// </summary>
    public decimal ReliquatCredit => Math.Max(0, Ecart - Math.Max(0, EncoursAReporter));

    public decimal? TauxEngagementPeriode => CalculsExecution.Taux(AEPeriode, PrevisionAjustee);
    public decimal? TauxEngagementYCReports => CalculsExecution.Taux(AETotal, DotationTotale);
    public decimal? TauxConsommationPeriode => CalculsExecution.Taux(PaiementsPeriode, PrevisionAjustee);
    public decimal? TauxConsommationYCReports => CalculsExecution.Taux(PaiementsTotal, DotationTotale);

    /// <summary>Paiements sur AD / AD (taux de consommation des AD).</summary>
    public decimal? TauxConsommationAD => CalculsExecution.Taux(PaiementsTotal, AETotal);

    public static MontantsDotation operator +(MontantsDotation a, MontantsDotation b) => new(
        a.MontantInitial + b.MontantInitial, a.Ajustements + b.Ajustements, a.Reports + b.Reports,
        a.AEPeriode + b.AEPeriode, a.AEReports + b.AEReports,
        a.PaiementsPeriode + b.PaiementsPeriode, a.PaiementsTotal + b.PaiementsTotal, a.RejetsPenalites + b.RejetsPenalites);

    public static MontantsDotation Somme(IEnumerable<MontantsDotation> montants) => montants.Aggregate(Zero, (a, b) => a + b);
}

/// <summary>Calculs et règles du cycle budgétaire commun (cahier fonctionnel § 3.4 et 4.1).</summary>
public static class CalculsExecution
{
    /// <summary>Taux en pourcentage, arrondi à 2 décimales ; null (« n.s. ») si le dénominateur est nul.</summary>
    public static decimal? Taux(decimal numerateur, decimal denominateur)
        => denominateur == 0 ? null : Math.Round(100m * numerateur / denominateur, 2, MidpointRounding.AwayFromZero);

    /// <summary>Montant au-delà de la dotation totale si l'on engage <paramref name="montant"/> en plus.</summary>
    public static decimal Depassement(MontantsDotation m, decimal montant) => Math.Max(0, m.AETotal + montant - m.DotationTotale);

    /// <summary>Taux d'engagement (y compris reports) après un engagement supplémentaire.</summary>
    public static decimal? TauxApres(MontantsDotation m, decimal montant) => Taux(m.AETotal + montant, m.DotationTotale);

    /// <summary>
    /// Reste disponible au paiement d'un engagement : montant − paiements − rejets ou
    /// pénalités. Un paiement ou un rejet ne peut pas le dépasser (§ 4.2).
    /// </summary>
    public static decimal ResteAPayer(decimal montantEngage, decimal paiements, decimal rejets) => montantEngage - paiements - rejets;

    /// <summary>
    /// Seuil d'alerte le plus élevé atteint par le taux, ou null. Les seuils propres à la
    /// ligne priment sur ceux de l'axe, qui priment sur les seuils globaux.
    /// </summary>
    public static decimal? SeuilAtteint(decimal? taux, IEnumerable<decimal> seuils)
        => taux is { } t ? seuils.Where(s => s <= t).Select(s => (decimal?)s).Max() : null;

    /// <summary>Numéro de pièce : préfixe, année, rang sur 5 chiffres (AJ-2026-00001).</summary>
    public static string Numero(string prefixe, int annee, int rang) => $"{prefixe}-{annee}-{rang:00000}";

    /// <summary>Rang suivant le plus grand numéro existant de la série (1 si aucun).</summary>
    public static int RangSuivant(string? dernierNumero)
        => dernierNumero is not null && int.TryParse(dernierNumero[(dernierNumero.LastIndexOf('-') + 1)..], out var rang) ? rang + 1 : 1;

    /// <summary>Montant en devise : positif ou nul selon l'usage, 2 décimales au plus (§ 4.3).</summary>
    public static bool EstMontantValide(decimal montant) => decimal.Round(montant, 2) == montant && Math.Abs(montant) < 10_000_000_000_000_000m;
}

/// <summary>Libellés des énumérations du cycle budgétaire, partagés par les écrans.</summary>
public static class LibellesExecution
{
    public static readonly IReadOnlyDictionary<StatutSaisie, string> Statut = new Dictionary<StatutSaisie, string>
    {
        [StatutSaisie.Brouillon] = "Brouillon",
        [StatutSaisie.Soumis]    = "Soumis",
        [StatutSaisie.Valide]    = "Validé",
        [StatutSaisie.Rejete]    = "Rejeté",
        [StatutSaisie.Annule]    = "Annulé",
    };

    public static readonly IReadOnlyDictionary<TypeEngagement, string> TypeEngagement = new Dictionary<TypeEngagement, string>
    {
        [Enums.TypeEngagement.AutorisationEngagement] = "Autorisation d'engagement (AE)",
        [Enums.TypeEngagement.AutorisationDepense]    = "Autorisation de dépense (AD)",
    };

    public static readonly IReadOnlyDictionary<TypeEngagement, string> TypeEngagementCourt = new Dictionary<TypeEngagement, string>
    {
        [Enums.TypeEngagement.AutorisationEngagement] = "AE",
        [Enums.TypeEngagement.AutorisationDepense]    = "AD",
    };

    public static readonly IReadOnlyDictionary<ModePaiement, string> ModePaiement = new Dictionary<ModePaiement, string>
    {
        [Enums.ModePaiement.Virement]     = "Virement",
        [Enums.ModePaiement.Cheque]       = "Chèque",
        [Enums.ModePaiement.Especes]      = "Espèces",
        [Enums.ModePaiement.Compensation] = "Compensation",
        [Enums.ModePaiement.Autre]        = "Autre",
    };

    public static readonly IReadOnlyDictionary<TypeReport, string> TypeReport = new Dictionary<TypeReport, string>
    {
        [Enums.TypeReport.ReliquatCredit]     = "Reliquat de crédit",
        [Enums.TypeReport.EncoursEngagement]  = "Encours d'engagement",
        [Enums.TypeReport.DepassementImpute]  = "Dépassement imputé",
    };

    public static readonly IReadOnlyDictionary<StatutValidation, string> StatutValidation = new Dictionary<StatutValidation, string>
    {
        [Enums.StatutValidation.EnAttente] = "En attente",
        [Enums.StatutValidation.Validee]   = "Validé",
        [Enums.StatutValidation.Rejetee]   = "Rejeté",
    };
}
