using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Vues de calcul, en lecture seule (§ 3.5)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Situation d'une dotation - vue bud.vw_SituationDotation (lecture seule, sans clé).
/// Base de tous les axes de dépenses, des alertes et du tableau de bord.
///
/// Conventions de calcul (cahier technique § 1.4 et § 2.8) :
///   - seules les pièces au statut Validé sont comptées (ajustements, engagements, paiements) ;
///   - la réalisation est mesurée par les paiements (C.2.2) ;
///   - les taux sont en pourcentage, sur deux bases (période seule, puis y compris
///     reports) ; un dénominateur nul donne null, affiché « n.s. ».
/// </summary>
public class SituationDotation : IEntiteOrganisme
{
    public Ulid DotationId { get; set; }
    public Ulid OrganismeId { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid AxeId { get; set; }
    public Ulid RubriqueId { get; set; }
    public Ulid SousRubriqueId { get; set; }
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid? NatureDepenseId { get; set; }
    public Ulid SourceFinancementId { get; set; }
    public Ulid? ProjetId { get; set; }
    public Ulid? SiteId { get; set; }
    public Ulid? StructureId { get; set; }
    public Ulid? TiersId { get; set; }

    /// <summary>Prévision initiale.</summary>
    public decimal MontantInitial { get; set; }
    /// <summary>Somme signée des ajustements validés.</summary>
    public decimal Ajustements { get; set; }
    /// <summary>Prévision initiale ± ajustements.</summary>
    public decimal PrevisionAjustee { get; set; }
    /// <summary>Reports reçus (reliquats, encours, dépassements imputés).</summary>
    public decimal Reports { get; set; }
    /// <summary>Prévision ajustée + reports.</summary>
    public decimal DotationTotale { get; set; }

    /// <summary>AE/AD de l'exercice (hors encours reportés).</summary>
    public decimal AEPeriode { get; set; }
    /// <summary>Encours reportés d'exercices antérieurs.</summary>
    public decimal AEReports { get; set; }
    public decimal AETotal { get; set; }

    /// <summary>Paiements sur les engagements de l'exercice.</summary>
    public decimal PaiementsPeriode { get; set; }
    /// <summary>Paiements y compris ceux des encours reportés.</summary>
    public decimal PaiementsTotal { get; set; }
    public decimal RejetsPenalites { get; set; }

    /// <summary>AE de la période − paiements correspondants.</summary>
    public decimal ResteAPayerPeriode { get; set; }
    /// <summary>AE totales − paiements totaux.</summary>
    public decimal ResteAPayerYCReports { get; set; }
    /// <summary>AE totales − paiements − rejets et pénalités (C.2.6).</summary>
    public decimal EncoursAReporter { get; set; }
    /// <summary>Écart budgétaire / reliquat : dotation totale − paiements totaux.</summary>
    public decimal Ecart { get; set; }

    public decimal? TauxEngagementPeriode { get; set; }
    public decimal? TauxEngagementYCReports { get; set; }
    public decimal? TauxConsommationPeriode { get; set; }
    public decimal? TauxConsommationYCReports { get; set; }
    /// <summary>Paiements / AE (taux de consommation des AD).</summary>
    public decimal? TauxConsommationAD { get; set; }
}

/// <summary>
/// Situation des dotations ventilée par activité, action et programme selon les clés de
/// répartition de l'exercice - vue prog.vw_ConsommationActivite (lecture seule, sans clé).
/// Une ligne par clé de répartition.
/// </summary>
public class ConsommationActivite : IEntiteOrganisme
{
    public Ulid OrganismeId { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid ProgrammeId { get; set; }
    public Ulid ActionId { get; set; }
    public Ulid ActiviteId { get; set; }
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid? NatureDepenseId { get; set; }
    public Ulid SourceFinancementId { get; set; }

    /// <summary>Part de la ligne affectée à l'activité.</summary>
    public decimal Pourcentage { get; set; }

    public decimal PrevisionAjustee { get; set; }
    public decimal DotationTotale { get; set; }
    public decimal AETotal { get; set; }
    public decimal PaiementsTotal { get; set; }
    public decimal ResteAPayerYCReports { get; set; }
}

/// <summary>
/// Atteinte des cibles d'un indicateur, par réalisation - vue prog.vw_AtteinteIndicateur
/// (lecture seule, sans clé). La cible retenue est celle du cycle le plus récent qui couvre
/// l'année de la période.
/// </summary>
public class AtteinteIndicateur : IEntiteOrganisme
{
    public Ulid IndicateurRealisationId { get; set; }
    public Ulid OrganismeId { get; set; }
    public Ulid IndicateurId { get; set; }
    public NiveauIndicateur Niveau { get; set; }
    public Ulid? ProgrammeId { get; set; }
    public Ulid? ActionId { get; set; }
    public Ulid PeriodeId { get; set; }
    public Ulid ExerciceId { get; set; }
    public int Annee { get; set; }
    public SensEvolution SensEvolution { get; set; }
    public StatutSaisie Statut { get; set; }

    public decimal ValeurRealisee { get; set; }
    public decimal? ValeurCible { get; set; }

    /// <summary>Valeur réalisée − cible.</summary>
    public decimal? EcartCible { get; set; }

    /// <summary>Réalisé / cible (hausse attendue) ou cible / réalisé (baisse attendue), en %.</summary>
    public decimal? TauxAtteinte { get; set; }

    /// <summary>null tant qu'aucune cible n'est définie.</summary>
    public bool? EstAtteinte { get; set; }
}

/// <summary>
/// Situation d'une prévision de recette - vue rec.vw_SituationRecette (lecture seule,
/// sans clé). Seuls les encaissements validés sont comptés.
/// </summary>
public class SituationRecette : IEntiteOrganisme
{
    public Ulid PrevisionRecetteId { get; set; }
    public Ulid OrganismeId { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid NatureRecetteId { get; set; }
    public Ulid? SiteId { get; set; }

    /// <summary>Prévision révisée si elle existe, sinon prévision initiale.</summary>
    public decimal PrevisionRetenue { get; set; }
    public decimal Encaisse { get; set; }

    /// <summary>Part encaissée positionnée sur un compte bancaire.</summary>
    public decimal EncaissePositionne { get; set; }

    /// <summary>Prévision retenue − encaissé.</summary>
    public decimal Ecart { get; set; }

    /// <summary>Encaissé / prévision retenue, en %.</summary>
    public decimal? TauxRecouvrement { get; set; }
}
