namespace eProcessBudget.Core.Enums;

// ═══════════════════════════════════════════════════════════════════════════════
//  Énumérations métier (cahier technique § 3.6) - paramètres et référentiels.
//  Valeurs PERSISTÉES en TINYINT : ne jamais renuméroter.
// ═══════════════════════════════════════════════════════════════════════════════

public enum StatutExercice : byte
{
    EnPreparation = 1,
    Ouvert = 2,
    Cloture = 3
}

public enum StatutCycle : byte
{
    Brouillon = 1,
    Valide = 2,
    Archive = 3
}

public enum TypePeriode : byte
{
    Mois = 1,
    Trimestre = 2,
    Semestre = 3,
    Annee = 4
}

public enum PorteeSeuil : byte
{
    Globale = 1,
    Axe = 2,
    LigneBudgetaire = 3
}

public enum TypeAxe : byte
{
    Depense = 1,
    Recette = 2
}

/// <summary>Section du budget voté (C.2.4 et C.2.12).</summary>
public enum Section : byte
{
    Fonctionnement = 1,
    Investissement = 2,
    HorsSections = 3
}

public enum TypeSite : byte
{
    Siege = 1,
    PostePeage = 2,
    PostePesage = 3,
    Autre = 4
}

public enum GroupeNature : byte
{
    DepensesOrdinaires = 1,
    DepensesEnCapital = 2
}

public enum TypeCompte : byte
{
    General = 1,
    Auxiliaire = 2
}

public enum SensLigne : byte
{
    Depense = 1,
    Recette = 2
}

public enum SensAjustement : byte
{
    Hausse = 1,
    Baisse = 2,
    HausseOuBaisse = 3
}

public enum TypeUnite : byte
{
    Pourcentage = 1,
    Nombre = 2,
    Duree = 3,
    Montant = 4,
    Distance = 5
}

public enum TypeTiers : byte
{
    EntiteBeneficiaireAppuis = 1,
    Prestataire = 2,
    PartenaireFinancier = 3,
    OrganismeCollecteur = 4,
    MaitreOuvrage = 5
}

public enum CategorieRecette : byte
{
    RecettePropre = 1,
    TaxeAffectee = 2,
    RessourceAdditionnelle = 3,
    RessourceExterne = 4,
    ReportRessourcesAnterieures = 5
}
