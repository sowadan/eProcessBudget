using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Recettes (schéma rec)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Prévision de recette par exercice, nature et (le cas échéant) site -
/// table rec.PrevisionRecette.
/// </summary>
public class PrevisionRecette : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }

    /// <summary>Ligne budgétaire de recette.</summary>
    public Ulid LigneBudgetaireId { get; set; }

    public Ulid NatureRecetteId { get; set; }

    /// <summary>Site (poste de péage ou de pesage).</summary>
    public Ulid? SiteId { get; set; }

    /// <summary>Organisme collecteur.</summary>
    public Ulid? TiersCollecteurId { get; set; }

    public decimal MontantInitial { get; set; }

    /// <summary>Prévision révisée ; quand elle existe, c'est elle qui est retenue.</summary>
    public decimal? MontantRevise { get; set; }

    public string? MotifRevision { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual LigneBudgetaire LigneBudgetaire { get; set; } = null!;
    public virtual NatureRecette NatureRecette { get; set; } = null!;
    public virtual Site? Site { get; set; }
    public virtual Tiers? TiersCollecteur { get; set; }
    public virtual ICollection<Encaissement> Encaissements { get; set; } = new List<Encaissement>();
    public virtual ICollection<EmissionRecette> Emissions { get; set; } = new List<EmissionRecette>();
    public virtual ICollection<EcheanceRecouvrement> Echeances { get; set; } = new List<EcheanceRecouvrement>();
}

/// <summary>Recette collectée (saisie mensuelle) - table rec.Encaissement.</summary>
public class Encaissement : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }

    /// <summary>Mois de rattachement.</summary>
    public Ulid PeriodeId { get; set; }

    public Ulid PrevisionRecetteId { get; set; }
    public DateOnly DateEncaissement { get; set; }
    public decimal Montant { get; set; }

    /// <summary>Compte crédité.</summary>
    public Ulid? CompteBancaireId { get; set; }

    /// <summary>Ressource positionnée sur un compte bancaire.</summary>
    public bool EstPositionne { get; set; }

    /// <summary>Référence de la pièce.</summary>
    public string? Reference { get; set; }

    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual Periode Periode { get; set; } = null!;
    public virtual PrevisionRecette PrevisionRecette { get; set; } = null!;
    public virtual CompteBancaire? CompteBancaire { get; set; }
}

/// <summary>
/// Facture émise (titre de recette, droit constaté) sur une prévision - table rec.EmissionRecette.
/// Compte sur chiffres (réponse C.2.9) : % d'exécution = émis / prévision, % de recouvrement
/// = recouvré (encaissé) / émis, reste à recouvrer = émis − recouvré.
/// </summary>
public class EmissionRecette : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }

    /// <summary>Mois de rattachement.</summary>
    public Ulid PeriodeId { get; set; }

    public Ulid PrevisionRecetteId { get; set; }
    public DateOnly DateEmission { get; set; }
    public decimal Montant { get; set; }

    /// <summary>Numéro de la facture ou du titre.</summary>
    public string? Reference { get; set; }

    public StatutSaisie Statut { get; set; } = StatutSaisie.Brouillon;

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual Periode Periode { get; set; } = null!;
    public virtual PrevisionRecette PrevisionRecette { get; set; } = null!;
}

/// <summary>Échéance de recouvrement d'une prévision de recette - table rec.EcheanceRecouvrement.</summary>
public class EcheanceRecouvrement : EntiteOrganisme
{
    public Ulid PrevisionRecetteId { get; set; }
    public DateOnly DateEcheance { get; set; }
    public decimal MontantAttendu { get; set; }

    public virtual PrevisionRecette PrevisionRecette { get; set; } = null!;
}

/// <summary>
/// Report de ressources antérieures (trésorerie sans déduction d'encours, fournisseurs
/// débiteurs, taxes et recettes antérieures, avoirs sur l'État) - table rec.ReportRessource.
/// </summary>
public class ReportRessource : EntiteOrganisme
{
    /// <summary>Exercice de destination.</summary>
    public Ulid ExerciceId { get; set; }

    /// <summary>Nature de report.</summary>
    public Ulid NatureRecetteId { get; set; }

    /// <summary>Montant constaté à la clôture de l'exercice précédent (réalisé).</summary>
    public decimal Montant { get; set; }

    /// <summary>Montant inscrit au budget (prévision ajustée du compte sur chiffres, C.2.9).</summary>
    public decimal? MontantPrevu { get; set; }

    /// <summary>Ressource positionnée sur un compte bancaire.</summary>
    public bool EstPositionne { get; set; }

    public string? Commentaire { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
    public virtual NatureRecette NatureRecette { get; set; } = null!;
}
