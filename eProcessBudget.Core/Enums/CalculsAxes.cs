namespace eProcessBudget.Core.Enums;

/// <summary>
/// Un axe de dépenses (cahier fonctionnel § 3.5 à 3.8) : le code de son ref.Axe, posé par
/// le provisionnement, et ses deux droits (cahier technique § 4.5).
/// </summary>
public sealed record AxeDepense(string Code, string Libelle, TypeDroit DroitLire, TypeDroit DroitSaisir)
{
    /// <summary>Droits qui ouvrent la consultation de l'axe : sa saisie donne aussi sa lecture.</summary>
    public TypeDroit[] Lecture => [DroitLire, DroitSaisir];
}

/// <summary>Les quatre axes de dépenses. Le code est celui de ref.Axe (ProvisionnementOrganisme).</summary>
public static class AxesDepense
{
    public static readonly AxeDepense Entretien = new("ENTRETIEN", "Entretien routier", TypeDroit.EntretienLire, TypeDroit.EntretienSaisir);
    public static readonly AxeDepense Investissement = new("INVESTISSEMENT", "Investissements", TypeDroit.InvestissementLire, TypeDroit.InvestissementSaisir);
    public static readonly AxeDepense Fonctionnement = new("FONCTIONNEMENT", "Fonctionnement", TypeDroit.FonctionnementLire, TypeDroit.FonctionnementSaisir);
    public static readonly AxeDepense Appuis = new("APPUIS", "Appuis", TypeDroit.AppuisLire, TypeDroit.AppuisSaisir);

    public static readonly IReadOnlyList<AxeDepense> Tous = [Entretien, Investissement, Fonctionnement, Appuis];

    /// <summary>Quiconque consulte au moins un axe.</summary>
    public static readonly TypeDroit[] LectureUnAxe = Tous.SelectMany(a => a.Lecture).ToArray();

    public static AxeDepense? DeCode(string? code) => Tous.FirstOrDefault(a => a.Code == code);
}

/// <summary>
/// Types de travaux d'entretien routier livrés (réponse du métier, C.2.11) : travaux courants,
/// travaux périodiques, travaux manuels d'entretien (cantonnage) et projets de réhabilitation.
/// La réhabilitation est un programme d'investissement (C.2.4) : ses opérations relèvent de
/// l'axe Investissements, les trois autres de l'axe Entretien routier.
/// </summary>
public static class CodesTypeTravaux
{
    public const string Courants = "COURANTS";
    public const string Periodiques = "PERIODIQUES";
    public const string Cantonnage = "CANTONNAGE";
    public const string Rehabilitation = "REHABILITATION";

    /// <summary>Types livrés avant la réponse du métier, désactivés s'ils ne servent pas.</summary>
    public static readonly IReadOnlyList<string> Anciens =
        ["ENT_REVETU", "ENT_NON_REVETU", "ENT_ASSAINISSEMENT", "ENT_PISTES", "URGENTS", "SECURITE"];
}

/// <summary>
/// Indicateurs de l'entretien routier (réponse du métier C.2.1). Le financement de l'entretien
/// est la situation des dotations de l'axe Entretien routier et des projets de réhabilitation
/// de l'axe Investissements (C.2.4).
/// </summary>
public sealed record IndicateursEntretien(MontantsDotation Financement, decimal BesoinsEvalues)
{
    /// <summary>Prévisions affectées : dotation totale (prévision ajustée + reports).</summary>
    public decimal PrevisionsAffectees => Financement.DotationTotale;

    /// <summary>Taux de conformité allocations / réalisations : AD / prévisions affectées.</summary>
    public decimal? TauxConformite => CalculsExecution.Taux(Financement.AETotal, PrevisionsAffectees);

    /// <summary>Taux de couverture des besoins : financement de l'entretien / besoins évalués.</summary>
    public decimal? TauxCouverture => CalculsExecution.Taux(PrevisionsAffectees, BesoinsEvalues);

    /// <summary>Taux d'exécution physique des programmes : crédits de paiement (payés) / AD.</summary>
    public decimal? TauxExecutionPhysique => Financement.TauxConsommationAD;
}

/// <summary>Formules propres aux axes de dépenses (cahier fonctionnel § 4.1).</summary>
public static class CalculsAxes
{
    /// <summary>Taux d'avancement kilométrique : km réalisés / km prévus ; « n.s. » sans km prévus.</summary>
    public static decimal? TauxAvancementKm(decimal kmRealises, decimal? kmPrevus)
        => CalculsExecution.Taux(kmRealises, kmPrevus ?? 0);

    /// <summary>Solde des appuis : montant prévu − montant versé.</summary>
    public static decimal SoldeAppui(decimal prevu, decimal verse) => prevu - verse;

    /// <summary>Montant d'un contrat : montant initial + avenants.</summary>
    public static decimal MontantContrat(decimal initial, decimal avenants) => initial + avenants;

    /// <summary>
    /// Part d'un montant revenant à une activité selon sa clé de répartition (en %), arrondie
    /// au centime. Les clés d'une ligne faisant 100 %, la somme des parts retombe sur le
    /// montant de la ligne, à l'arrondi près.
    /// </summary>
    public static decimal Part(decimal montant, decimal pourcentage)
        => Math.Round(montant * pourcentage / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Applique une clé de répartition à tous les montants d'une situation.</summary>
    public static MontantsDotation Repartir(MontantsDotation m, decimal pourcentage) => new(
        Part(m.MontantInitial, pourcentage), Part(m.Ajustements, pourcentage), Part(m.Reports, pourcentage),
        Part(m.AEPeriode, pourcentage), Part(m.AEReports, pourcentage),
        Part(m.PaiementsPeriode, pourcentage), Part(m.PaiementsTotal, pourcentage), Part(m.RejetsPenalites, pourcentage));

    /// <summary>Un appui validé est « en attente de versement » s'il reste à verser, passé ce délai après sa validation.</summary>
    public const int DelaiAppuiEnAttenteJours = 30;
}

/// <summary>Libellés des énumérations des axes de dépenses, partagés par les écrans.</summary>
public static class LibellesAxes
{
    public static readonly IReadOnlyDictionary<StatutProjet, string> StatutProjet = new Dictionary<StatutProjet, string>
    {
        [Enums.StatutProjet.Programme] = "Programmé",
        [Enums.StatutProjet.EnCours]   = "En cours",
        [Enums.StatutProjet.Acheve]    = "Achevé",
        [Enums.StatutProjet.Suspendu]  = "Suspendu",
        [Enums.StatutProjet.Annule]    = "Annulé",
    };

    public static readonly IReadOnlyDictionary<TypeContrat, string> TypeContrat = new Dictionary<TypeContrat, string>
    {
        [Enums.TypeContrat.Travaux]           = "Travaux",
        [Enums.TypeContrat.Prestation]        = "Prestation (études, contrôle, surveillance, qualité)",
        [Enums.TypeContrat.FournitureService] = "Fourniture / service",
        [Enums.TypeContrat.ConventionAppui]   = "Convention d'appui",
    };

    public static readonly IReadOnlyDictionary<EtatImmobilisation, string> EtatImmobilisation = new Dictionary<EtatImmobilisation, string>
    {
        [Enums.EtatImmobilisation.Bon]         = "Bon",
        [Enums.EtatImmobilisation.Moyen]       = "Moyen",
        [Enums.EtatImmobilisation.HorsService] = "Hors service",
        [Enums.EtatImmobilisation.CedeReforme] = "Cédé / réformé",
    };

    /// <summary>Types de contrat admis sur chaque axe.</summary>
    public static IReadOnlyList<TypeContrat> TypesContrat(string codeAxe) => codeAxe switch
    {
        "ENTRETIEN" => [Enums.TypeContrat.Travaux, Enums.TypeContrat.Prestation],
        "APPUIS" => [Enums.TypeContrat.ConventionAppui],
        _ => [Enums.TypeContrat.Travaux, Enums.TypeContrat.Prestation, Enums.TypeContrat.FournitureService],
    };
}
