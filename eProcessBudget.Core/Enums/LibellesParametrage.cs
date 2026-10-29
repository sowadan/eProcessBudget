namespace eProcessBudget.Core.Enums;

/// <summary>
/// Libellés des énumérations du Paramétrage (cahier technique § 3.6), partagés par les
/// écrans et les fichiers Excel. Un libellé sert aussi à l'import : ne le changez pas à la
/// légère, ou acceptez que les anciens fichiers ne se réimportent plus.
/// </summary>
public static class LibellesParametrage
{
    public static readonly IReadOnlyDictionary<TypeAxe, string> TypeAxe = new Dictionary<TypeAxe, string>
    {
        [Enums.TypeAxe.Depense] = "Dépense",
        [Enums.TypeAxe.Recette] = "Recette",
    };

    public static readonly IReadOnlyDictionary<Section, string> Section = new Dictionary<Section, string>
    {
        [Enums.Section.Fonctionnement] = "Fonctionnement",
        [Enums.Section.Investissement] = "Investissement",
        [Enums.Section.HorsSections]   = "Hors sections",
    };

    public static readonly IReadOnlyDictionary<TypeSite, string> TypeSite = new Dictionary<TypeSite, string>
    {
        [Enums.TypeSite.Siege]       = "Siège",
        [Enums.TypeSite.PostePeage]  = "Poste de péage",
        [Enums.TypeSite.PostePesage] = "Poste de pesage",
        [Enums.TypeSite.Autre]       = "Autre",
    };

    public static readonly IReadOnlyDictionary<GroupeNature, string> GroupeNature = new Dictionary<GroupeNature, string>
    {
        [Enums.GroupeNature.DepensesOrdinaires] = "Dépenses ordinaires",
        [Enums.GroupeNature.DepensesEnCapital]  = "Dépenses en capital",
    };

    public static readonly IReadOnlyDictionary<TypeCompte, string> TypeCompte = new Dictionary<TypeCompte, string>
    {
        [Enums.TypeCompte.General]    = "Général",
        [Enums.TypeCompte.Auxiliaire] = "Auxiliaire",
    };

    public static readonly IReadOnlyDictionary<SensLigne, string> SensLigne = new Dictionary<SensLigne, string>
    {
        [Enums.SensLigne.Depense] = "Dépense",
        [Enums.SensLigne.Recette] = "Recette",
    };

    public static readonly IReadOnlyDictionary<SensAjustement, string> SensAjustement = new Dictionary<SensAjustement, string>
    {
        [Enums.SensAjustement.Hausse]         = "Hausse",
        [Enums.SensAjustement.Baisse]         = "Baisse",
        [Enums.SensAjustement.HausseOuBaisse] = "Hausse ou baisse",
    };

    public static readonly IReadOnlyDictionary<TypeUnite, string> TypeUnite = new Dictionary<TypeUnite, string>
    {
        [Enums.TypeUnite.Pourcentage] = "Pourcentage",
        [Enums.TypeUnite.Nombre]      = "Nombre",
        [Enums.TypeUnite.Duree]       = "Durée",
        [Enums.TypeUnite.Montant]     = "Montant",
        [Enums.TypeUnite.Distance]    = "Distance",
    };

    public static readonly IReadOnlyDictionary<TypeTiers, string> TypeTiers = new Dictionary<TypeTiers, string>
    {
        [Enums.TypeTiers.EntiteBeneficiaireAppuis] = "Entité bénéficiaire d'appuis",
        [Enums.TypeTiers.Prestataire]              = "Prestataire / entreprise",
        [Enums.TypeTiers.PartenaireFinancier]      = "Partenaire financier",
        [Enums.TypeTiers.OrganismeCollecteur]      = "Organisme collecteur",
        [Enums.TypeTiers.MaitreOuvrage]            = "Maître d'ouvrage / administration",
    };

    public static readonly IReadOnlyDictionary<CategorieRecette, string> CategorieRecette = new Dictionary<CategorieRecette, string>
    {
        [Enums.CategorieRecette.RecettePropre]               = "Recette propre",
        [Enums.CategorieRecette.TaxeAffectee]                = "Taxe affectée",
        [Enums.CategorieRecette.RessourceAdditionnelle]      = "Ressource additionnelle",
        [Enums.CategorieRecette.RessourceExterne]            = "Ressource externe",
        [Enums.CategorieRecette.ReportRessourcesAnterieures] = "Report de ressources antérieures",
    };

    public static readonly IReadOnlyDictionary<PorteeSeuil, string> PorteeSeuil = new Dictionary<PorteeSeuil, string>
    {
        [Enums.PorteeSeuil.Globale]         = "Globale",
        [Enums.PorteeSeuil.Axe]             = "Axe",
        [Enums.PorteeSeuil.LigneBudgetaire] = "Ligne budgétaire",
    };

    public static readonly IReadOnlyDictionary<StatutExercice, string> StatutExercice = new Dictionary<StatutExercice, string>
    {
        [Enums.StatutExercice.EnPreparation] = "En préparation",
        [Enums.StatutExercice.Ouvert]        = "Ouvert",
        [Enums.StatutExercice.Cloture]       = "Clôturé",
    };

    public static readonly IReadOnlyDictionary<StatutCycle, string> StatutCycle = new Dictionary<StatutCycle, string>
    {
        [Enums.StatutCycle.Brouillon] = "Brouillon",
        [Enums.StatutCycle.Valide]    = "Validé",
        [Enums.StatutCycle.Archive]   = "Archivé",
    };

    public static readonly IReadOnlyDictionary<TypePeriode, string> TypePeriode = new Dictionary<TypePeriode, string>
    {
        [Enums.TypePeriode.Mois]      = "Mois",
        [Enums.TypePeriode.Trimestre] = "Trimestre",
        [Enums.TypePeriode.Semestre]  = "Semestre",
        [Enums.TypePeriode.Annee]     = "Année",
    };

    /// <summary>Libellé d'une valeur, ou son nom si aucun libellé n'est déclaré.</summary>
    public static string De<TEnum>(IReadOnlyDictionary<TEnum, string> libelles, TEnum valeur) where TEnum : struct, Enum
        => libelles.TryGetValue(valeur, out var l) ? l : valeur.ToString();
}
