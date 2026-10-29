using eProcessBudget.Core.Enums;

namespace eProcessBudget.Infrastructure.Services.Rapports;

/// <summary>Paramètres qu'un état demande à l'écran.</summary>
[Flags]
public enum ParametresDemandes
{
    Aucun = 0,
    Exercice = 1,
    Axe = 2,

    /// <summary>Période facultative (mois, trimestre, semestre) : situation à sa date de fin.</summary>
    Periode = 4,

    /// <summary>Trimestre obligatoire.</summary>
    Trimestre = 8,

    /// <summary>Semestre obligatoire.</summary>
    Semestre = 16,

    Cycle = 32,

    /// <summary>Programme facultatif.</summary>
    Programme = 64,

    /// <summary>Plage de dates (du / au).</summary>
    Dates = 128,
}

/// <summary>Valeurs saisies pour produire un état.</summary>
public sealed class ParametresRapport
{
    public Ulid? ExerciceId { get; set; }
    public Ulid? AxeId { get; set; }
    public Ulid? PeriodeId { get; set; }
    public Ulid? CycleId { get; set; }
    public Ulid? ProgrammeId { get; set; }
    public DateOnly? Du { get; set; }
    public DateOnly? Au { get; set; }
}

/// <summary>Un état du catalogue.</summary>
/// <param name="Droits">Droits de lecture des données de l'état : il en faut au moins un, en plus de RapportExporter.</param>
public sealed record DescripteurRapport(
    string Code, string Famille, string Titre, string Description, ParametresDemandes Parametres, TypeDroit[] Droits, bool Paysage = false);

/// <summary>
/// Catalogue des états (cahier fonctionnel § 3.12, technique § 4.8). Chacun exige le droit
/// RapportExporter et un droit de lecture de ses données ; ses données viennent des services
/// des modules, avec leurs propres contrôles (droits, périmètre de lecture).
/// </summary>
public static class CatalogueRapports
{
    public const string FamilleGestion = "Rapports de gestion budgétaire";
    public const string FamilleBudgetProgramme = "Documents du budget-programme";

    private static readonly TypeDroit[] Consommation = [TypeDroit.ConsommationLire];
    private static readonly TypeDroit[] BudgetProgramme =
        [TypeDroit.BudgetProgrammeLire, TypeDroit.BudgetProgrammeDefinir, TypeDroit.ProgrammationSaisir, TypeDroit.IndicateurSaisirRealisation];

    public static readonly IReadOnlyList<DescripteurRapport> Tous =
    [
        // ── Gestion budgétaire (§ 3.12.1) ──────────────────────────────────
        new("SITUATION_AXE", FamilleGestion, "Situation budgétaire par axe",
            "Dotations de l'axe par rubrique : prévisions initiale et ajustée, dotation totale, engagé, payé, reste à payer, taux.",
            ParametresDemandes.Exercice | ParametresDemandes.Axe | ParametresDemandes.Periode, Consommation, Paysage: true),
        new("SYNTHESE_GLOBALE", FamilleGestion, "Synthèse globale (tous axes)",
            "Tableau consolidé de tous les axes : prévisions, engagements, factures, paiements, reliquats, projection ; recettes en regard.",
            ParametresDemandes.Exercice | ParametresDemandes.Periode, Consommation, Paysage: true),
        new("CONSOMMATION_RUBRIQUE_COMPTE", FamilleGestion, "Consommation par rubrique et par compte",
            "Consommation des crédits par rubrique budgétaire puis par compte comptable principal des lignes.",
            ParametresDemandes.Exercice | ParametresDemandes.Axe | ParametresDemandes.Periode, Consommation, Paysage: true),
        new("AVANCEMENT_TRAVAUX", FamilleGestion, "Avancement des travaux routiers",
            "Indicateurs de l'entretien (conformité, couverture des besoins, exécution physique) ; opérations d'entretien et de réhabilitation : kilométrage, avancement, exécution financière.",
            ParametresDemandes.Exercice, [TypeDroit.EntretienLire, TypeDroit.EntretienSaisir], Paysage: true),
        new("INVESTISSEMENTS_SITES", FamilleGestion, "Investissements par site",
            "Situation des investissements par site et valeur de l'inventaire des immobilisations.",
            ParametresDemandes.Exercice, [TypeDroit.InvestissementLire, TypeDroit.InvestissementSaisir], Paysage: true),
        new("APPUIS", FamilleGestion, "Appuis financiers accordés et versés",
            "Appuis par entité bénéficiaire : prévu, engagé, versé, solde ; appuis en attente de versement.",
            ParametresDemandes.Exercice, [TypeDroit.AppuisLire, TypeDroit.AppuisSaisir], Paysage: true),
        new("BILAN", FamilleGestion, "Bilan recettes / dépenses",
            "Ressources (recettes par catégorie, reports) face aux dépenses par axe, en prévision et en réalisation.",
            ParametresDemandes.Exercice, [TypeDroit.RecettesLire, TypeDroit.PrevisionRecetteSaisir, TypeDroit.EncaissementSaisir]),
        new("COMPTE_CHIFFRES_RECETTES", FamilleGestion, "Compte sur chiffres : recettes",
            "Par ligne : prévision ajustée, factures émises (% d'exécution), recouvrées (% de recouvrement), reste à recouvrer ; reports de ressources ; totaux par section.",
            ParametresDemandes.Exercice, [TypeDroit.RecettesLire, TypeDroit.PrevisionRecetteSaisir, TypeDroit.EncaissementSaisir], Paysage: true),
        new("COMPTE_CHIFFRES_DEPENSES", FamilleGestion, "Compte sur chiffres : dépenses",
            "Par chapitre : prévision ajustée, prise en charge (factures reçues), rejets ou pénalités, paiements, reste à payer ; engagements antérieurs reportés.",
            ParametresDemandes.Exercice, Consommation, Paysage: true),
        new("EQUILIBRE_TRESORERIE", FamilleGestion, "Équilibre de trésorerie et reports",
            "Grandeurs A à I : recettes recouvrées, engagements payés, encours, trésorerie disponible, reports de ressources, contrôle H = I ; exercice et précédent.",
            ParametresDemandes.Exercice, Consommation),
        new("ETAT_PRESENTATION_BUDGET", FamilleGestion, "État de présentation du budget (N / N-1)",
            "Lignes de recettes et chapitres de dépenses des deux exercices : prévisions, réalisations N-1, variations ; commentaires par rubrique des variations significatives.",
            ParametresDemandes.Exercice, Consommation, Paysage: true),
        new("PROJECTION", FamilleGestion, "Projection budgétaire",
            "Projection des paiements en fin d'exercice par axe (extrapolation linéaire) et dotations qui dérapent.",
            ParametresDemandes.Exercice | ParametresDemandes.Periode, Consommation, Paysage: true),
        new("AUDIT", FamilleGestion, "Journal des modifications",
            "Historique d'audit des opérations sur une période : date, utilisateur, module, action, élément.",
            ParametresDemandes.Dates, [TypeDroit.AuditConsultation], Paysage: true),

        // ── Budget-programme (§ 3.12.2) ────────────────────────────────────
        new("DPPD", FamilleBudgetProgramme, "Document de programmation pluriannuelle des dépenses (DPPD)",
            "Coût par programme, action et nature de dépense : AE et CP de l'exercice de référence et des trois années, total triennal.",
            ParametresDemandes.Cycle, BudgetProgramme, Paysage: true),
        new("RESSOURCES", FamilleBudgetProgramme, "Ressources de financement des programmes",
            "Ressources programmées par nature et source sur le moyen terme, et équilibre ressources / emplois.",
            ParametresDemandes.Cycle, BudgetProgramme, Paysage: true),
        new("INDICATEURS", FamilleBudgetProgramme, "Indicateurs principaux et cibles",
            "Indicateurs des programmes avec leur valeur de référence et leurs cibles, global ou pour un programme.",
            ParametresDemandes.Cycle | ParametresDemandes.Programme, BudgetProgramme, Paysage: true),
        new("CADRE_LOGIQUE", FamilleBudgetProgramme, "Cadre logique par programme",
            "Objectifs, résultats, indicateurs, sources de vérification et risques de chaque programme.",
            ParametresDemandes.Cycle | ParametresDemandes.Programme, BudgetProgramme, Paysage: true),
        new("EMPLOIS", FamilleBudgetProgramme, "Emplois par catégorie",
            "Effectifs programmés par catégorie d'emplois, global et par programme, avec les crédits de personnel.",
            ParametresDemandes.Cycle, BudgetProgramme, Paysage: true),
        new("EXECUTION_TROIS_EXERCICES", FamilleBudgetProgramme, "Exécution des trois derniers exercices",
            "Prévision et réalisation par nature de dépense sur l'exercice et les deux précédents.",
            ParametresDemandes.Exercice, Consommation, Paysage: true),
        new("RAPPORT_TRIMESTRIEL", FamilleBudgetProgramme, "Rapport financier trimestriel",
            "Situation à fin de trimestre par axe et par nature, engagements et paiements du trimestre, recettes.",
            ParametresDemandes.Exercice | ParametresDemandes.Trimestre, Consommation, Paysage: true),
        new("RAPPORT_SEMESTRIEL", FamilleBudgetProgramme, "Rapport semestriel d'exécution des programmes",
            "Exécution par programme et par action à fin de semestre, AE / CP, réalisations des indicateurs du semestre.",
            ParametresDemandes.Exercice | ParametresDemandes.Semestre, Consommation, Paysage: true),
        new("RAPPORT_ANNUEL_PERFORMANCE", FamilleBudgetProgramme, "Rapport annuel de performance",
            "Par programme : coûts programmés et exécutés, atteinte des indicateurs, analyses qualitatives (défis, recommandations).",
            ParametresDemandes.Exercice, Consommation, Paysage: true),
    ];

    public static DescripteurRapport? Obtenir(string code) => Tous.FirstOrDefault(r => r.Code == code);
}
