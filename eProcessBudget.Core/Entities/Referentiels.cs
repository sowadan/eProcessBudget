using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Référentiels du Paramétrage (schéma ref)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Valeur d'une liste simple paramétrable - table ref.ListeValeur. Les listes sont
/// distinguées par <see cref="Categorie"/> (voir <see cref="CategoriesListeValeur"/>).
/// </summary>
public class ListeValeur : EntiteOrganisme, IActivable
{
    public string Categorie { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public bool EstActif { get; set; } = true;
}

/// <summary>Catégories de <see cref="ListeValeur"/> prévues par le cahier technique (§ 3.3.4).</summary>
public static class CategoriesListeValeur
{
    public const string TypeVoie = "TYPE_VOIE";
    public const string TypePrestation = "TYPE_PRESTATION";
    public const string TypeStructure = "TYPE_STRUCTURE";
    public const string CategorieEntite = "CATEGORIE_ENTITE";
    public const string SourceVerification = "SOURCE_VERIFICATION";
    public const string CategorieRisque = "CATEGORIE_RISQUE";
    public const string TypePenalite = "TYPE_PENALITE";
    public const string Periodicite = "PERIODICITE";

    /// <summary>Catégories connues et leur libellé, dans l'ordre d'affichage.</summary>
    public static readonly IReadOnlyList<(string Code, string Libelle)> Toutes =
    [
        (TypeStructure, "Types de structure"),
        (TypeVoie, "Types de voie"),
        (TypePrestation, "Types de prestation"),
        (CategorieEntite, "Catégories d'entités bénéficiaires"),
        (SourceVerification, "Sources de vérification"),
        (CategorieRisque, "Catégories de risques"),
        (TypePenalite, "Types de pénalité"),
        (Periodicite, "Périodicités"),
    ];

    public static string Libelle(string categorie)
        => Toutes.FirstOrDefault(c => c.Code == categorie).Libelle ?? categorie;

    public static bool EstConnue(string categorie) => Toutes.Any(c => c.Code == categorie);
}

/// <summary>
/// Axe budgétaire (Entretien routier, Investissements, Fonctionnement, Appuis, Recettes) -
/// table ref.Axe.
/// </summary>
public class Axe : EntiteOrganisme, IActivable
{
    /// <summary>ENTRETIEN, INVEST, FONCT, APPUIS, RECETTES.</summary>
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;
    public TypeAxe TypeAxe { get; set; }

    /// <summary>Section du budget voté (C.2.4).</summary>
    public Section Section { get; set; }

    public int Ordre { get; set; }

    public virtual ICollection<Rubrique> Rubriques { get; set; } = new List<Rubrique>();
}

/// <summary>Structure ou service de l'organisme (hiérarchie) - table ref.Structure.</summary>
public class Structure : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string? Sigle { get; set; }
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Type (liste TYPE_STRUCTURE).</summary>
    public Ulid? TypeStructureId { get; set; }

    public Ulid? StructureParentId { get; set; }
    public string? ResponsableNom { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual ListeValeur? TypeStructure { get; set; }
    public virtual Structure? StructureParent { get; set; }
}

/// <summary>Site géographique : siège, poste de péage, poste de pesage - table ref.Site.</summary>
public class Site : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public TypeSite TypeSite { get; set; }
    public string? Region { get; set; }
    public string? Prefecture { get; set; }
    public string? Commune { get; set; }
    public string? AxeRoutier { get; set; }
    public DateOnly? DateMiseEnService { get; set; }

    /// <summary>Compte tiers (auxiliaire) qui lie le poste à ses recettes (réponse C.2.13).</summary>
    public Ulid? CompteTiersId { get; set; }

    public bool EstActif { get; set; } = true;

    public virtual CompteComptable? CompteTiers { get; set; }

    /// <summary>Collectivités locales de la zone d'implantation (C.2.8) ; une collectivité n'est rattachée qu'à un poste.</summary>
    public virtual ICollection<SiteTiers> Collectivites { get; set; } = new List<SiteTiers>();
}

/// <summary>
/// Rattachement d'une collectivité locale (tiers) à un site (C.2.8) - table allégée ref.SiteTiers.
/// </summary>
public class SiteTiers : EntiteLiaison
{
    public Ulid SiteId { get; set; }
    public Ulid TiersId { get; set; }

    public virtual Site Site { get; set; } = null!;
    public virtual Tiers Tiers { get; set; } = null!;
}

/// <summary>Catégorie d'emplois (HC, CS, CM, AM, AE, contractuels...) - table ref.CategorieEmploi.</summary>
public class CategorieEmploi : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;
    public int Ordre { get; set; }
}

/// <summary>
/// Agent de l'organisme, utilisé pour la répartition du temps par action et activité -
/// table ref.Agent. Aucune donnée de rémunération n'est stockée.
/// </summary>
public class Agent : EntiteOrganisme, IActivable
{
    public string Matricule { get; set; } = string.Empty;
    public string NomComplet { get; set; } = string.Empty;
    public Ulid CategorieEmploiId { get; set; }
    public Ulid StructureId { get; set; }

    /// <summary>Site d'affectation.</summary>
    public Ulid? SiteId { get; set; }

    public bool EstActif { get; set; } = true;

    public virtual CategorieEmploi CategorieEmploi { get; set; } = null!;
    public virtual Structure Structure { get; set; } = null!;
    public virtual Site? Site { get; set; }
}

/// <summary>
/// Nature de dépense : 1 personnel, 2 biens et services, 3 transferts courants,
/// 4 investissements RI, 5 investissements RE - table ref.NatureDepense.
/// </summary>
public class NatureDepense : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;
    public GroupeNature GroupeNature { get; set; }
    public int Ordre { get; set; }
}

/// <summary>Source de financement : ressources internes (RI) ou externes (RE) - table ref.SourceFinancement.</summary>
public class SourceFinancement : EntiteOrganisme, IActivable
{
    /// <summary>RI, RE.</summary>
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;

    /// <summary>Partenaire financier (ressources externes).</summary>
    public Ulid? PartenaireFinancierId { get; set; }

    public virtual Tiers? PartenaireFinancier { get; set; }
}

/// <summary>Compte du plan comptable, général ou auxiliaire - table ref.CompteComptable.</summary>
public class CompteComptable : EntiteOrganisme, IActivable
{
    public string Numero { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Classe (1 à 9).</summary>
    public int Classe { get; set; }

    public TypeCompte TypeCompte { get; set; }

    /// <summary>Compte de regroupement.</summary>
    public Ulid? CompteParentId { get; set; }

    public bool EstImmobilisation { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual CompteComptable? CompteParent { get; set; }
}

/// <summary>Rubrique budgétaire (chapitre), rattachée à un axe - table ref.Rubrique.</summary>
public class Rubrique : EntiteOrganisme, IActivable
{
    public Ulid AxeId { get; set; }
    public string Code { get; set; } = string.Empty;

    /// <summary>Repère de présentation (a, b, c...).</summary>
    public string? Note { get; set; }

    public string Libelle { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual Axe Axe { get; set; } = null!;
    public virtual ICollection<SousRubrique> SousRubriques { get; set; } = new List<SousRubrique>();
}

/// <summary>Sous-rubrique budgétaire - table ref.SousRubrique.</summary>
public class SousRubrique : EntiteOrganisme, IActivable
{
    public Ulid RubriqueId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public int Ordre { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual Rubrique Rubrique { get; set; } = null!;
    public virtual ICollection<LigneBudgetaire> Lignes { get; set; } = new List<LigneBudgetaire>();
}

/// <summary>
/// Ligne budgétaire : niveau de saisie des prévisions et de l'exécution -
/// table ref.LigneBudgetaire. Une ligne de dépense porte une nature de dépense, une
/// ligne de recette une nature de recette (contrainte CHECK).
/// </summary>
public class LigneBudgetaire : EntiteOrganisme, IActivable
{
    public Ulid SousRubriqueId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public SensLigne Sens { get; set; }

    /// <summary>Obligatoire pour une ligne de dépense.</summary>
    public Ulid? NatureDepenseId { get; set; }

    /// <summary>Obligatoire pour une ligne de recette.</summary>
    public Ulid? NatureRecetteId { get; set; }

    public Ulid SourceFinancementId { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual SousRubrique SousRubrique { get; set; } = null!;
    public virtual NatureDepense? NatureDepense { get; set; }
    public virtual NatureRecette? NatureRecette { get; set; }
    public virtual SourceFinancement SourceFinancement { get; set; } = null!;

    /// <summary>Comptes comptables d'imputation (au moins un, règle 4.2).</summary>
    public virtual ICollection<LigneCompte> Comptes { get; set; } = new List<LigneCompte>();
}

/// <summary>Correspondance ligne budgétaire ↔ compte comptable - table allégée ref.LigneCompte.</summary>
public class LigneCompte : EntiteLiaison
{
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid CompteComptableId { get; set; }

    /// <summary>Compte principal d'imputation.</summary>
    public bool EstPrincipal { get; set; }

    public virtual LigneBudgetaire LigneBudgetaire { get; set; } = null!;
    public virtual CompteComptable CompteComptable { get; set; } = null!;
}

/// <summary>Motif d'ajustement budgétaire - table ref.MotifAjustement.</summary>
public class MotifAjustement : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;
    public SensAjustement Sens { get; set; } = SensAjustement.HausseOuBaisse;
}

/// <summary>Unité de mesure des indicateurs - table ref.UniteMesure.</summary>
public class UniteMesure : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;
    public TypeUnite TypeUnite { get; set; }

    /// <summary>%, j, km...</summary>
    public string? Symbole { get; set; }
}

/// <summary>Risque du catalogue utilisé par le cadre logique - table ref.Risque.</summary>
public class Risque : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;

    /// <summary>Catégorie (liste CATEGORIE_RISQUE).</summary>
    public Ulid? CategorieRisqueId { get; set; }

    public virtual ListeValeur? CategorieRisque { get; set; }
}

/// <summary>
/// Type de travaux, ou prestation de contrôle associée à un type de travaux -
/// table ref.TypeTravaux.
/// </summary>
public class TypeTravaux : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;

    /// <summary>Type de voie (liste TYPE_VOIE).</summary>
    public Ulid? TypeVoieId { get; set; }

    /// <summary>true pour une ligne de contrôle (et non de travaux).</summary>
    public bool EstPrestationControle { get; set; }

    /// <summary>Travaux contrôlés, pour une prestation de contrôle.</summary>
    public Ulid? TypeTravauxControleId { get; set; }

    public int Ordre { get; set; }

    public virtual ListeValeur? TypeVoie { get; set; }
    public virtual TypeTravaux? TypeTravauxControle { get; set; }
}

/// <summary>
/// Tiers : entité bénéficiaire d'appuis, prestataire, partenaire financier, organisme
/// collecteur ou maître d'ouvrage - table ref.Tiers.
/// </summary>
public class Tiers : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public TypeTiers TypeTiers { get; set; }

    /// <summary>Catégorie (liste CATEGORIE_ENTITE).</summary>
    public Ulid? CategorieEntiteId { get; set; }

    public string RaisonSociale { get; set; } = string.Empty;
    public string? Sigle { get; set; }

    /// <summary>Entité de rattachement d'une sous-entité (C.2.8).</summary>
    public Ulid? TiersParentId { get; set; }

    public string? IdentifiantFiscal { get; set; }
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public Ulid? CompteGeneralId { get; set; }
    public Ulid? CompteAuxiliaireId { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual ListeValeur? CategorieEntite { get; set; }
    public virtual Tiers? TiersParent { get; set; }
    public virtual CompteComptable? CompteGeneral { get; set; }
    public virtual CompteComptable? CompteAuxiliaire { get; set; }
}

/// <summary>
/// Nature de recette (hiérarchie) : péage, pesage, loyers, taxes affectées, reports... -
/// table ref.NatureRecette.
/// </summary>
public class NatureRecette : EntiteOrganisme, IActivable
{
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public bool EstActif { get; set; } = true;
    public CategorieRecette CategorieRecette { get; set; }

    /// <summary>Nature de regroupement.</summary>
    public Ulid? NatureParentId { get; set; }

    /// <summary>Compte de produit.</summary>
    public Ulid? CompteComptableId { get; set; }

    /// <summary>Saisie par site (péage, pesage).</summary>
    public bool SuiviParSite { get; set; }

    public virtual NatureRecette? NatureParent { get; set; }
    public virtual CompteComptable? CompteComptable { get; set; }
}

/// <summary>Compte bancaire de l'organisme (positionnement des ressources) - table ref.CompteBancaire.</summary>
public class CompteBancaire : EntiteOrganisme, IActivable
{
    public string Banque { get; set; } = string.Empty;
    public string Intitule { get; set; } = string.Empty;
    public string NumeroCompte { get; set; } = string.Empty;
    public Ulid DeviseId { get; set; }

    /// <summary>Compte de trésorerie.</summary>
    public Ulid? CompteComptableId { get; set; }

    public bool EstActif { get; set; } = true;

    public virtual Devise Devise { get; set; } = null!;
    public virtual CompteComptable? CompteComptable { get; set; }
}
