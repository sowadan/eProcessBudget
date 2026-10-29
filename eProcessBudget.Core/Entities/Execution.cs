using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Cycle budgétaire et exécution des dépenses (schéma bud)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Projet ou opération budgétaire (travaux, investissement, appui) - table bud.Projet.
/// </summary>
public class Projet : EntiteOrganisme
{
    public Ulid AxeId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Intitule { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Ulid? SiteId { get; set; }

    /// <summary>Service.</summary>
    public Ulid? StructureId { get; set; }

    /// <summary>Bénéficiaire (axe Appuis).</summary>
    public Ulid? TiersBeneficiaireId { get; set; }

    /// <summary>Source de financement (axe Investissements).</summary>
    public Ulid? SourceFinancementId { get; set; }

    public DateOnly? DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
    public bool EstPluriannuel { get; set; }
    public StatutProjet Statut { get; set; } = StatutProjet.Programme;

    public virtual Axe Axe { get; set; } = null!;
    public virtual Site? Site { get; set; }
    public virtual Structure? Structure { get; set; }
    public virtual Tiers? TiersBeneficiaire { get; set; }
    public virtual SourceFinancement? SourceFinancement { get; set; }

    /// <summary>Complément propre à l'axe Entretien routier (relation 1-1).</summary>
    public virtual ProjetTravaux? Travaux { get; set; }

    public virtual ICollection<Contrat> Contrats { get; set; } = new List<Contrat>();
}

/// <summary>
/// Complément d'un projet de l'axe Entretien routier - table bud.ProjetTravaux.
/// Relation 1-1 : <see cref="BaseEntity.Id"/> est égal à l'Id du <see cref="Projet"/>
/// (clé primaire et clé étrangère), il n'est donc pas généré.
/// </summary>
public class ProjetTravaux : EntiteOrganisme
{
    /// <summary>Type de voie (liste TYPE_VOIE).</summary>
    public Ulid? TypeVoieId { get; set; }

    public Ulid? TypeTravauxId { get; set; }

    /// <summary>Maître d'ouvrage (C.2.5).</summary>
    public Ulid? MaitreOuvrageId { get; set; }

    public string? Localisation { get; set; }
    public decimal? KmPrevus { get; set; }

    public virtual Projet Projet { get; set; } = null!;
    public virtual ListeValeur? TypeVoie { get; set; }
    public virtual TypeTravaux? TypeTravaux { get; set; }
    public virtual Tiers? MaitreOuvrage { get; set; }
}

/// <summary>
/// Contrat ou marché rattaché à un projet (travaux, prestation, convention d'appui) -
/// table bud.Contrat.
/// </summary>
public class Contrat : EntiteOrganisme
{
    public Ulid ProjetId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Objet { get; set; } = string.Empty;
    public TypeContrat TypeContrat { get; set; }

    /// <summary>Type de prestation (liste TYPE_PRESTATION).</summary>
    public Ulid? TypePrestationId { get; set; }

    /// <summary>Contrat de travaux contrôlé ou suivi, pour une prestation.</summary>
    public Ulid? ContratTravauxId { get; set; }

    public Ulid TitulaireId { get; set; }
    public decimal MontantInitial { get; set; }

    /// <summary>Total des avenants.</summary>
    public decimal MontantAvenants { get; set; }

    public DateOnly? DateSignature { get; set; }
    public DateOnly? DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }

    /// <summary>Contrat pluriannuel : AE sur la durée du contrat, CP annuels.</summary>
    public bool EstPluriannuel { get; set; }

    public decimal? KmContractuels { get; set; }
    public StatutProjet Statut { get; set; } = StatutProjet.Programme;

    public virtual Projet Projet { get; set; } = null!;
    public virtual ListeValeur? TypePrestation { get; set; }
    public virtual Contrat? ContratTravaux { get; set; }
    public virtual Tiers Titulaire { get; set; } = null!;
}

/// <summary>
/// Prévision initiale d'une ligne budgétaire pour un exercice - table bud.Dotation.
/// Grain : ligne × projet × site × structure × tiers. Toute la situation (ajustements,
/// reports, AE, paiements, taux) est calculée par la vue bud.vw_SituationDotation.
/// </summary>
public class Dotation : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }
    public Ulid LigneBudgetaireId { get; set; }
    public Ulid? ProjetId { get; set; }
    public Ulid? SiteId { get; set; }

    /// <summary>Service.</summary>
    public Ulid? StructureId { get; set; }

    /// <summary>Entité bénéficiaire (axe Appuis).</summary>
    public Ulid? TiersId { get; set; }

    /// <summary>Prévision initiale.</summary>
    public decimal MontantInitial { get; set; }

    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual LigneBudgetaire LigneBudgetaire { get; set; } = null!;
    public virtual Projet? Projet { get; set; }
    public virtual Site? Site { get; set; }
    public virtual Structure? Structure { get; set; }
    public virtual Tiers? Tiers { get; set; }
    public virtual ICollection<Ajustement> Ajustements { get; set; } = new List<Ajustement>();
    public virtual ICollection<Engagement> Engagements { get; set; } = new List<Engagement>();
}

/// <summary>Ajustement d'une dotation (hausse ou baisse), motif obligatoire - table bud.Ajustement.</summary>
public class Ajustement : EntiteOrganisme
{
    public Ulid DotationId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateOnly DateAjustement { get; set; }

    /// <summary>Montant signé : positif pour une hausse, négatif pour une baisse (jamais 0).</summary>
    public decimal Montant { get; set; }

    public Ulid MotifAjustementId { get; set; }
    public string Justification { get; set; } = string.Empty;
    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;
    public Ulid? ValideParId { get; set; }
    public DateTime? DateValidation { get; set; }

    public virtual Dotation Dotation { get; set; } = null!;
    public virtual MotifAjustement MotifAjustement { get; set; } = null!;
}

/// <summary>
/// Report d'un exercice sur le suivant : reliquat de crédit, encours d'engagement ou
/// dépassement imputé (report négatif) - table bud.Report.
/// </summary>
public class Report : EntiteOrganisme
{
    public Ulid ExerciceSourceId { get; set; }
    public Ulid ExerciceCibleId { get; set; }
    public Ulid? DotationSourceId { get; set; }
    public Ulid DotationCibleId { get; set; }

    /// <summary>Engagement reporté (encours).</summary>
    public Ulid? EngagementSourceId { get; set; }

    public TypeReport TypeReport { get; set; }
    public OrigineReport Origine { get; set; }

    /// <summary>Montant ; négatif pour un dépassement imputé.</summary>
    public decimal Montant { get; set; }

    public string? Commentaire { get; set; }

    public virtual Exercice ExerciceSource { get; set; } = null!;
    public virtual Exercice ExerciceCible { get; set; } = null!;
    public virtual Dotation? DotationSource { get; set; }
    public virtual Dotation DotationCible { get; set; } = null!;
    public virtual Engagement? EngagementSource { get; set; }
}

/// <summary>
/// Autorisation d'engagement (AE) ou de dépense (AD) sur une dotation - table bud.Engagement.
/// Les engagements reportés d'un exercice antérieur portent <see cref="EstReport"/> = true.
/// </summary>
public class Engagement : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }
    public Ulid DotationId { get; set; }
    public Ulid? ContratId { get; set; }

    /// <summary>Bénéficiaire.</summary>
    public Ulid? TiersId { get; set; }

    public TypeEngagement TypeEngagement { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateOnly DateEngagement { get; set; }
    public string Objet { get; set; } = string.Empty;
    public decimal Montant { get; set; }

    /// <summary>Encours reporté d'un exercice antérieur.</summary>
    public bool EstReport { get; set; }

    /// <summary>Engagement d'origine, pour un encours reporté.</summary>
    public Ulid? EngagementOrigineId { get; set; }

    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;

    /// <summary>Validateur (Responsable Budget).</summary>
    public Ulid? ValideParId { get; set; }

    public DateTime? DateValidation { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual Dotation Dotation { get; set; } = null!;
    public virtual Contrat? Contrat { get; set; }
    public virtual Tiers? Tiers { get; set; }
    public virtual Engagement? EngagementOrigine { get; set; }
    public virtual ICollection<EcheancierCP> EcheancierCP { get; set; } = new List<EcheancierCP>();
    public virtual ICollection<Facture> Factures { get; set; } = new List<Facture>();
    public virtual ICollection<Paiement> Paiements { get; set; } = new List<Paiement>();
    public virtual ICollection<RejetPenalite> RejetsPenalites { get; set; } = new List<RejetPenalite>();
}

/// <summary>
/// Crédit de paiement annuel d'un engagement pluriannuel - table bud.EcheancierCP.
/// La somme des CP d'un engagement reste ≤ montant de l'AE (contrôle applicatif).
/// </summary>
public class EcheancierCP : EntiteOrganisme
{
    public Ulid EngagementId { get; set; }
    public int Annee { get; set; }
    public decimal MontantCP { get; set; }

    public virtual Engagement Engagement { get; set; } = null!;
}

/// <summary>
/// Demande de validation d'un dépassement de dotation - table bud.ValidationDepassement.
/// Non bloquant : l'engagement reste Soumis jusqu'à la décision du Responsable Budget.
/// </summary>
public class ValidationDepassement : EntiteOrganisme
{
    public Ulid DotationId { get; set; }

    /// <summary>Engagement à l'origine du dépassement.</summary>
    public Ulid EngagementId { get; set; }

    /// <summary>Montant au-delà de la dotation totale.</summary>
    public decimal MontantDepassement { get; set; }

    /// <summary>Taux de consommation après engagement, en pourcentage.</summary>
    public decimal TauxApres { get; set; }

    public string Motif { get; set; } = string.Empty;
    public StatutValidation Statut { get; set; } = StatutValidation.EnAttente;
    public Ulid? DecideParId { get; set; }
    public DateTime? DateDecision { get; set; }
    public string? CommentaireDecision { get; set; }

    public virtual Dotation Dotation { get; set; } = null!;
    public virtual Engagement Engagement { get; set; } = null!;
}

/// <summary>Facture reçue sur un engagement (C.2.2) - table bud.Facture.</summary>
public class Facture : EntiteOrganisme
{
    public Ulid EngagementId { get; set; }

    /// <summary>Numéro fournisseur.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateOnly DateFacture { get; set; }
    public DateOnly DateReception { get; set; }

    /// <summary>Montant TTC.</summary>
    public decimal Montant { get; set; }

    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;

    public virtual Engagement Engagement { get; set; } = null!;
}

/// <summary>
/// Paiement, y compris versement d'appui - table bud.Paiement.
/// La somme des paiements d'un engagement reste ≤ montant de l'engagement.
/// </summary>
public class Paiement : EntiteOrganisme
{
    /// <summary>Exercice du paiement (peut différer de celui d'un engagement reporté).</summary>
    public Ulid ExerciceId { get; set; }

    public Ulid EngagementId { get; set; }
    public Ulid? FactureId { get; set; }
    public string NumeroPiece { get; set; } = string.Empty;

    /// <summary>Date du paiement, comprise dans l'exercice actif.</summary>
    public DateOnly DatePaiement { get; set; }

    public decimal Montant { get; set; }
    public ModePaiement ModePaiement { get; set; }

    /// <summary>Compte débité.</summary>
    public Ulid? CompteBancaireId { get; set; }

    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual Engagement Engagement { get; set; } = null!;
    public virtual Facture? Facture { get; set; }
    public virtual CompteBancaire? CompteBancaire { get; set; }
}

/// <summary>Rejet ou pénalité d'exécution venant réduire l'encours (C.2.6) - table bud.RejetPenalite.</summary>
public class RejetPenalite : EntiteOrganisme
{
    public Ulid EngagementId { get; set; }

    /// <summary>Type (liste TYPE_PENALITE).</summary>
    public Ulid TypePenaliteId { get; set; }

    public DateOnly DateConstat { get; set; }
    public decimal Montant { get; set; }
    public string Motif { get; set; } = string.Empty;

    public virtual Engagement Engagement { get; set; } = null!;
    public virtual ListeValeur TypePenalite { get; set; } = null!;
}

/// <summary>
/// Avancement physique d'un projet ou d'un contrat sur une période (kilomètres
/// entretenus par type de travaux, taux) - table bud.AvancementPhysique.
/// </summary>
public class AvancementPhysique : EntiteOrganisme
{
    public Ulid ProjetId { get; set; }
    public Ulid? ContratId { get; set; }
    public Ulid PeriodeId { get; set; }

    /// <summary>Type de travaux (C.2.11).</summary>
    public Ulid? TypeTravauxId { get; set; }

    public decimal? KmRealises { get; set; }

    /// <summary>Taux d'avancement physique, en pourcentage.</summary>
    public decimal? TauxAvancement { get; set; }

    public string? Commentaire { get; set; }

    public virtual Projet Projet { get; set; } = null!;
    public virtual Contrat? Contrat { get; set; }
    public virtual Periode Periode { get; set; } = null!;
    public virtual TypeTravaux? TypeTravaux { get; set; }
}

/// <summary>
/// Besoin d'entretien routier évalué pour un exercice, global ou par type de travaux -
/// table bud.BesoinEntretien. Dénominateur du taux de couverture des besoins (C.2.1) :
/// financement de l'entretien / besoins évalués.
/// </summary>
public class BesoinEntretien : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }

    /// <summary>Type de travaux ; null pour un besoin global.</summary>
    public Ulid? TypeTravauxId { get; set; }

    public decimal Montant { get; set; }

    /// <summary>Kilométrage à entretenir correspondant, facultatif.</summary>
    public decimal? Km { get; set; }

    /// <summary>Source de l'évaluation (étude, programme d'entretien...).</summary>
    public string? Commentaire { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual TypeTravaux? TypeTravaux { get; set; }
}

/// <summary>Immobilisation ou équipement inventorié par site - table bud.Immobilisation.</summary>
public class Immobilisation : EntiteOrganisme
{
    /// <summary>Code inventaire.</summary>
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;

    /// <summary>Projet d'acquisition.</summary>
    public Ulid? ProjetId { get; set; }

    public Ulid SiteId { get; set; }

    /// <summary>Service.</summary>
    public Ulid? StructureId { get; set; }

    /// <summary>Compte d'immobilisation.</summary>
    public Ulid CompteComptableId { get; set; }

    public DateOnly DateAcquisition { get; set; }
    public decimal ValeurAcquisition { get; set; }
    public EtatImmobilisation Etat { get; set; } = EtatImmobilisation.Bon;

    public virtual Projet? Projet { get; set; }
    public virtual Site Site { get; set; } = null!;
    public virtual Structure? Structure { get; set; }
    public virtual CompteComptable CompteComptable { get; set; } = null!;
}

/// <summary>
/// Enveloppe d'entretien allouée à un maître d'ouvrage pour une rubrique -
/// table bud.EnveloppeMaitreOuvrage. Prévue pour C.2.5, inactive par défaut.
/// </summary>
public class EnveloppeMaitreOuvrage : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }
    public Ulid MaitreOuvrageId { get; set; }
    public Ulid RubriqueId { get; set; }

    /// <summary>Enveloppe allouée.</summary>
    public decimal Montant { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual Tiers MaitreOuvrage { get; set; } = null!;
    public virtual Rubrique Rubrique { get; set; } = null!;
}
