using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Budget-programme et performance (schéma prog)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Programme du budget-programme - table prog.Programme.</summary>
public class Programme : EntiteOrganisme, IActivable
{
    /// <summary>P1, P2...</summary>
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Objectif général (stratégique).</summary>
    public string ObjectifGeneral { get; set; } = string.Empty;

    /// <summary>Texte de présentation du programme.</summary>
    public string? Presentation { get; set; }

    /// <summary>Responsable de programme : une seule personne (réponse du métier, C.1).</summary>
    public Ulid ResponsableId { get; set; }

    public int Ordre { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual ICollection<ActionProgramme> Actions { get; set; } = new List<ActionProgramme>();

    /// <summary>Structures responsables conjointes.</summary>
    public virtual ICollection<ProgrammeStructure> Structures { get; set; } = new List<ProgrammeStructure>();
}

/// <summary>Structure responsable conjointe d'un programme - table allégée prog.ProgrammeStructure.</summary>
public class ProgrammeStructure : EntiteLiaison
{
    public Ulid ProgrammeId { get; set; }
    public Ulid StructureId { get; set; }
    public bool EstPrincipale { get; set; }

    public virtual Programme Programme { get; set; } = null!;
    public virtual Structure Structure { get; set; } = null!;
}

/// <summary>
/// Action d'un programme (nombre libre) - table prog.Action.
/// La classe est nommée ActionProgramme pour ne pas masquer le délégué System.Action.
/// </summary>
public class ActionProgramme : EntiteOrganisme, IActivable
{
    public Ulid ProgrammeId { get; set; }

    /// <summary>1.1, 1.2...</summary>
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Objectif spécifique (opérationnel).</summary>
    public string ObjectifSpecifique { get; set; } = string.Empty;

    /// <summary>Structure responsable.</summary>
    public Ulid? StructureId { get; set; }

    public int Ordre { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual Programme Programme { get; set; } = null!;
    public virtual Structure? Structure { get; set; }
    public virtual ICollection<Activite> Activites { get; set; } = new List<Activite>();
}

/// <summary>Activité d'une action - table prog.Activite.</summary>
public class Activite : EntiteOrganisme, IActivable
{
    public Ulid ActionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Structure responsable.</summary>
    public Ulid? StructureId { get; set; }

    public Ulid? NatureDepensePrincipaleId { get; set; }
    public bool EstActif { get; set; } = true;

    public virtual ActionProgramme Action { get; set; } = null!;
    public virtual Structure? Structure { get; set; }
    public virtual NatureDepense? NatureDepensePrincipale { get; set; }
}

/// <summary>
/// Part d'une ligne budgétaire affectée à une activité pour un exercice -
/// table prog.CleRepartition. Pour une ligne et un exercice, la somme des pourcentages
/// vaut 100 % (contrôle applicatif, § 3.4).
/// </summary>
public class CleRepartition : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid ActiviteId { get; set; }

    /// <summary>Part en pourcentage, dans ]0 ; 100].</summary>
    public decimal Pourcentage { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual LigneBudgetaire LigneBudgetaire { get; set; } = null!;
    public virtual Activite Activite { get; set; } = null!;
}

/// <summary>
/// Indicateur de performance de niveau programme, action ou résultat immédiat -
/// table prog.Indicateur.
/// </summary>
public class Indicateur : EntiteOrganisme, IActivable
{
    public NiveauIndicateur Niveau { get; set; }

    /// <summary>Renseigné pour le niveau Programme (et seulement lui).</summary>
    public Ulid? ProgrammeId { get; set; }

    /// <summary>Renseigné pour les niveaux Action et Extrant.</summary>
    public Ulid? ActionId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public Ulid UniteMesureId { get; set; }

    /// <summary>Mode de calcul, en texte libre.</summary>
    public string? ModeCalcul { get; set; }

    /// <summary>Formule automatique, si l'indicateur est calculé par l'application (C.2.1).</summary>
    public string? CodeFormule { get; set; }

    public SensEvolution SensEvolution { get; set; } = SensEvolution.HausseAttendue;

    /// <summary>Source de vérification (liste SOURCE_VERIFICATION).</summary>
    public Ulid? SourceVerificationId { get; set; }

    /// <summary>Périodicité de mesure (liste PERIODICITE).</summary>
    public Ulid? PeriodiciteId { get; set; }

    /// <summary>Structure responsable.</summary>
    public Ulid? StructureId { get; set; }

    public int? AnneeReference { get; set; }
    public decimal? ValeurReference { get; set; }

    /// <summary>Un indicateur auquel des données sont rattachées se désactive au lieu d'être supprimé (§ 4.2).</summary>
    public bool EstActif { get; set; } = true;

    public virtual Programme? Programme { get; set; }
    public virtual ActionProgramme? Action { get; set; }
    public virtual UniteMesure UniteMesure { get; set; } = null!;
    public virtual ListeValeur? SourceVerification { get; set; }
    public virtual ListeValeur? Periodicite { get; set; }
    public virtual Structure? Structure { get; set; }
    public virtual ICollection<IndicateurCible> Cibles { get; set; } = new List<IndicateurCible>();
    public virtual ICollection<IndicateurRealisation> Realisations { get; set; } = new List<IndicateurRealisation>();
}

/// <summary>Cible annuelle d'un indicateur pour un cycle de programmation - table prog.IndicateurCible.</summary>
public class IndicateurCible : EntiteOrganisme
{
    public Ulid IndicateurId { get; set; }
    public Ulid CycleProgrammationId { get; set; }

    /// <summary>Année N, N+1 ou N+2 du cycle.</summary>
    public int Annee { get; set; }

    public decimal ValeurCible { get; set; }

    public virtual Indicateur Indicateur { get; set; } = null!;
    public virtual CycleProgrammation CycleProgrammation { get; set; } = null!;
}

/// <summary>
/// Valeur réalisée d'un indicateur sur une période - table prog.IndicateurRealisation.
/// Saisie par le service financier sur transmission du responsable de programme.
/// </summary>
public class IndicateurRealisation : EntiteOrganisme
{
    public Ulid IndicateurId { get; set; }
    public Ulid PeriodeId { get; set; }
    public decimal ValeurRealisee { get; set; }
    public string? Commentaire { get; set; }
    public string? JustificationEcart { get; set; }

    /// <summary>Responsable ayant transmis la donnée.</summary>
    public Ulid? TransmisParId { get; set; }

    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;

    public virtual Indicateur Indicateur { get; set; } = null!;
    public virtual Periode Periode { get; set; } = null!;
}

/// <summary>
/// Élément du cadre logique d'un programme pour un cycle (objectif général, objectif
/// spécifique, résultat immédiat) - table prog.CadreLogiqueElement.
/// </summary>
public class CadreLogiqueElement : EntiteOrganisme
{
    public Ulid ProgrammeId { get; set; }
    public Ulid CycleProgrammationId { get; set; }
    public TypeElementCL TypeElement { get; set; }

    /// <summary>Action, pour un objectif spécifique.</summary>
    public Ulid? ActionId { get; set; }

    public Ulid? ElementParentId { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public Ulid? IndicateurId { get; set; }

    /// <summary>Source de vérification (liste SOURCE_VERIFICATION).</summary>
    public Ulid? SourceVerificationId { get; set; }

    /// <summary>Valeur cible, pour un résultat immédiat.</summary>
    public decimal? ValeurCible { get; set; }

    public int Ordre { get; set; }

    public virtual Programme Programme { get; set; } = null!;
    public virtual CycleProgrammation CycleProgrammation { get; set; } = null!;
    public virtual ActionProgramme? Action { get; set; }
    public virtual CadreLogiqueElement? ElementParent { get; set; }
    public virtual Indicateur? Indicateur { get; set; }
    public virtual ListeValeur? SourceVerification { get; set; }
    public virtual ICollection<CadreLogiqueRisque> Risques { get; set; } = new List<CadreLogiqueRisque>();
}

/// <summary>Risque associé à un élément du cadre logique - table allégée prog.CadreLogiqueRisque.</summary>
public class CadreLogiqueRisque : EntiteLiaison
{
    public Ulid CadreLogiqueElementId { get; set; }
    public Ulid RisqueId { get; set; }

    public virtual CadreLogiqueElement CadreLogiqueElement { get; set; } = null!;
    public virtual Risque Risque { get; set; } = null!;
}

/// <summary>
/// AE et CP programmés par année, action, activité, nature de dépense et source -
/// table prog.ProgrammationFinanciere.
/// </summary>
public class ProgrammationFinanciere : EntiteOrganisme
{
    public Ulid CycleProgrammationId { get; set; }

    /// <summary>Année N-1 à N+2 du cycle.</summary>
    public int Annee { get; set; }

    public Ulid ActionId { get; set; }
    public Ulid? ActiviteId { get; set; }
    public Ulid NatureDepenseId { get; set; }
    public Ulid SourceFinancementId { get; set; }

    /// <summary>Autorisations d'engagement.</summary>
    public decimal MontantAE { get; set; }

    /// <summary>Crédits de paiement.</summary>
    public decimal MontantCP { get; set; }

    public virtual CycleProgrammation CycleProgrammation { get; set; } = null!;
    public virtual ActionProgramme Action { get; set; } = null!;
    public virtual Activite? Activite { get; set; }
    public virtual NatureDepense NatureDepense { get; set; } = null!;
    public virtual SourceFinancement SourceFinancement { get; set; } = null!;
}

/// <summary>Ressource programmée pour le financement des programmes - table prog.ProgrammationRessource.</summary>
public class ProgrammationRessource : EntiteOrganisme
{
    public Ulid CycleProgrammationId { get; set; }
    public int Annee { get; set; }
    public Ulid NatureRecetteId { get; set; }
    public Ulid SourceFinancementId { get; set; }
    public decimal Montant { get; set; }

    public virtual CycleProgrammation CycleProgrammation { get; set; } = null!;
    public virtual NatureRecette NatureRecette { get; set; } = null!;
    public virtual SourceFinancement SourceFinancement { get; set; } = null!;
}

/// <summary>Effectif programmé par programme et catégorie d'emplois - table prog.ProgrammationEmploi.</summary>
public class ProgrammationEmploi : EntiteOrganisme
{
    public Ulid CycleProgrammationId { get; set; }
    public int Annee { get; set; }
    public Ulid ProgrammeId { get; set; }
    public Ulid CategorieEmploiId { get; set; }
    public int Effectif { get; set; }

    public virtual CycleProgrammation CycleProgrammation { get; set; } = null!;
    public virtual Programme Programme { get; set; } = null!;
    public virtual CategorieEmploi CategorieEmploi { get; set; } = null!;
}

/// <summary>
/// Part du temps d'un agent consacrée à une action (et le cas échéant une activité) -
/// table prog.RepartitionTemps. Base de ventilation des charges de personnel et des
/// charges communes. Pour un agent et un exercice, la somme reste ≤ 100 %.
/// </summary>
public class RepartitionTemps : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }
    public Ulid AgentId { get; set; }
    public Ulid ActionId { get; set; }
    public Ulid? ActiviteId { get; set; }

    /// <summary>Part du temps de travail en pourcentage, dans ]0 ; 100].</summary>
    public decimal PourcentageTemps { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual Agent Agent { get; set; } = null!;
    public virtual ActionProgramme Action { get; set; } = null!;
    public virtual Activite? Activite { get; set; }
}

/// <summary>
/// Analyse rédigée : contrainte, priorité, défi, recommandation, commentaire de rubrique -
/// table prog.AnalyseQualitative.
/// </summary>
public class AnalyseQualitative : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }
    public TypeAnalyse TypeAnalyse { get; set; }
    public Ulid? ProgrammeId { get; set; }

    /// <summary>Rubrique commentée (TypeAnalyse = CommentaireRubrique).</summary>
    public Ulid? RubriqueId { get; set; }

    public string Contenu { get; set; } = string.Empty;
    public int Ordre { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual Programme? Programme { get; set; }
    public virtual Rubrique? Rubrique { get; set; }
}
