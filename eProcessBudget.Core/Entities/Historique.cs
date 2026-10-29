using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Historique (schéma hist)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Exécution d'un exercice antérieur, reprise par import ou calculée à la clôture -
/// table hist.ExecutionHistorique. Base des états pluriannuels.
/// </summary>
public class ExecutionHistorique : EntiteOrganisme
{
    public int Annee { get; set; }
    public Ulid? AxeId { get; set; }
    public Ulid? RubriqueId { get; set; }
    public Ulid? NatureDepenseId { get; set; }
    public Ulid? NatureRecetteId { get; set; }
    public Ulid? SiteId { get; set; }
    public decimal Prevision { get; set; }

    /// <summary>Montant engagé ou autorisé.</summary>
    public decimal? MontantAutorise { get; set; }

    /// <summary>Réalisé (payé ou encaissé).</summary>
    public decimal? MontantRealise { get; set; }

    /// <summary>Restant à payer à la clôture.</summary>
    public decimal? RestantAPayer { get; set; }

    public decimal? KmEntretenus { get; set; }
    public SourceHistorique Source { get; set; }

    public virtual Axe? Axe { get; set; }
    public virtual Rubrique? Rubrique { get; set; }
    public virtual NatureDepense? NatureDepense { get; set; }
    public virtual NatureRecette? NatureRecette { get; set; }
    public virtual Site? Site { get; set; }
}
