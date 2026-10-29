using eProcessBudget.Core.Enums;

namespace eProcessBudget.Core.Entities;

// ═══════════════════════════════════════════════════════════════════════════════
//  Paramètres généraux (schéma cfg)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Paramètres généraux et techniques d'un organisme (une ligne par organisme) -
/// table cfg.ParametreOrganisme. Les valeurs par défaut sont celles du § 3.7.
/// </summary>
public class ParametreOrganisme : EntiteOrganisme
{
    public Ulid? DeviseReferenceId { get; set; }
    public int NbDecimales { get; set; } = 2;
    public int DureeSessionMinutes { get; set; } = 30;
    public int NbTentativesMax { get; set; } = 5;
    public int TaillePagination { get; set; } = 20;

    /// <summary>Durée de conservation des journaux, au moins 12 mois.</summary>
    public int RetentionJournauxMois { get; set; } = 12;

    public Ulid? LogoFichierId { get; set; }
    public string? EnteteRapport { get; set; }
    public string? PiedPageRapport { get; set; }

    public virtual Devise? DeviseReference { get; set; }
    public virtual Fichier? LogoFichier { get; set; }
}

/// <summary>Fichier stocké en base (logo, pièce justificative, import) - table cfg.Fichier.</summary>
public class Fichier : EntiteOrganisme
{
    public string NomFichier { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Taille en octets.</summary>
    public int Taille { get; set; }

    /// <summary>SHA-256 du contenu, en hexadécimal.</summary>
    public string Empreinte { get; set; } = string.Empty;

    public byte[] Contenu { get; set; } = [];
}

/// <summary>Devise utilisée par l'organisme - table cfg.Devise.</summary>
public class Devise : EntiteOrganisme
{
    /// <summary>Code ISO 4217 (XOF...).</summary>
    public string CodeIso { get; set; } = string.Empty;
    public string Libelle { get; set; } = string.Empty;
    public string? Symbole { get; set; }
    public bool EstReference { get; set; }
}

/// <summary>Taux de change vers la devise de référence (ressources externes) - table cfg.TauxChange.</summary>
public class TauxChange : EntiteOrganisme
{
    public Ulid DeviseId { get; set; }
    public DateOnly DateEffet { get; set; }

    /// <summary>1 unité de la devise = Taux unités de la devise de référence.</summary>
    public decimal Taux { get; set; }

    public virtual Devise Devise { get; set; } = null!;
}

/// <summary>
/// Cycle de programmation triennale glissante (N-1 référence, N base, N+1, N+2) -
/// table cfg.CycleProgrammation.
/// </summary>
public class CycleProgrammation : EntiteOrganisme
{
    /// <summary>Ex. « Programmation 2027-2029 ».</summary>
    public string Libelle { get; set; } = string.Empty;

    /// <summary>Année N-1.</summary>
    public int AnneeReference { get; set; }
    /// <summary>Année N.</summary>
    public int AnneeBase { get; set; }
    /// <summary>Année N+2.</summary>
    public int AnneeFin { get; set; }

    public StatutCycle Statut { get; set; } = StatutCycle.Brouillon;

    /// <summary>Cycle reconduit (reconduction glissante).</summary>
    public Ulid? CyclePrecedentId { get; set; }

    public virtual CycleProgrammation? CyclePrecedent { get; set; }
}

/// <summary>Exercice budgétaire annuel - table cfg.Exercice.</summary>
public class Exercice : EntiteOrganisme
{
    public int Annee { get; set; }
    public DateOnly DateDebut { get; set; }
    public DateOnly DateFin { get; set; }
    public StatutExercice Statut { get; set; } = StatutExercice.EnPreparation;

    /// <summary>Cycle dont l'exercice est l'année de base.</summary>
    public Ulid? CycleProgrammationId { get; set; }

    public DateTime? DateOuverture { get; set; }
    public DateTime? DateCloture { get; set; }
    public Ulid? ClotureParId { get; set; }

    public virtual CycleProgrammation? CycleProgrammation { get; set; }
    public virtual ICollection<Periode> Periodes { get; set; } = new List<Periode>();
}

/// <summary>
/// Période infra-annuelle d'un exercice (12 mois, 4 trimestres, 2 semestres, l'année) -
/// table cfg.Periode. Générée à la création de l'exercice.
/// </summary>
public class Periode : EntiteOrganisme
{
    public Ulid ExerciceId { get; set; }
    public TypePeriode TypePeriode { get; set; }

    /// <summary>Rang dans l'exercice (1 à 12 pour les mois, 1 à 4 pour les trimestres...).</summary>
    public int Numero { get; set; }

    public DateOnly DateDebut { get; set; }
    public DateOnly DateFin { get; set; }

    /// <summary>Période fermée à la saisie.</summary>
    public bool EstCloturee { get; set; }

    public virtual Exercice Exercice { get; set; } = null!;
}

/// <summary>Seuil d'alerte de consommation (80 / 90 / 100 % par défaut) - table cfg.SeuilAlerte.</summary>
public class SeuilAlerte : EntiteOrganisme
{
    public PorteeSeuil Portee { get; set; } = PorteeSeuil.Globale;

    /// <summary>Renseigné si <see cref="Portee"/> = Axe.</summary>
    public Ulid? AxeId { get; set; }

    /// <summary>Renseigné si <see cref="Portee"/> = LigneBudgetaire.</summary>
    public Ulid? LigneBudgetaireId { get; set; }

    /// <summary>Seuil en pourcentage (ex. 80.0000).</summary>
    public decimal Pourcentage { get; set; }

    /// <summary>Couleur d'affichage (#RRGGBB).</summary>
    public string? Couleur { get; set; }

    public virtual Axe? Axe { get; set; }
    public virtual LigneBudgetaire? LigneBudgetaire { get; set; }
}
