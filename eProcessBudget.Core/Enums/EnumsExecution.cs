namespace eProcessBudget.Core.Enums;

// ═══════════════════════════════════════════════════════════════════════════════
//  Énumérations métier (cahier technique § 3.6) - cycle budgétaire, recettes, historique.
//  Valeurs PERSISTÉES en TINYINT : ne jamais renuméroter.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Cycle de vie commun des pièces saisies (dotation, engagement, paiement...).</summary>
public enum StatutSaisie : byte
{
    Brouillon = 1,
    Soumis = 2,
    Valide = 3,
    Rejete = 4,
    Annule = 5
}

public enum StatutProjet : byte
{
    Programme = 1,
    EnCours = 2,
    Acheve = 3,
    Suspendu = 4,
    Annule = 5
}

public enum TypeContrat : byte
{
    Travaux = 1,
    Prestation = 2,
    FournitureService = 3,
    ConventionAppui = 4
}

public enum TypeEngagement : byte
{
    /// <summary>Autorisation d'engagement (AE).</summary>
    AutorisationEngagement = 1,
    /// <summary>Autorisation de dépense (AD).</summary>
    AutorisationDepense = 2
}

public enum TypeReport : byte
{
    ReliquatCredit = 1,
    EncoursEngagement = 2,
    /// <summary>Report négatif d'un dépassement (C.2.5) - désactivé par défaut.</summary>
    DepassementImpute = 3
}

public enum OrigineReport : byte
{
    ClotureAutomatique = 1,
    SaisieManuelle = 2
}

public enum StatutValidation : byte
{
    EnAttente = 1,
    Validee = 2,
    Rejetee = 3
}

public enum ModePaiement : byte
{
    Virement = 1,
    Cheque = 2,
    Especes = 3,
    Compensation = 4,
    Autre = 5
}

public enum EtatImmobilisation : byte
{
    Bon = 1,
    Moyen = 2,
    HorsService = 3,
    CedeReforme = 4
}

public enum SourceHistorique : byte
{
    /// <summary>Reprise de l'existant (import Excel).</summary>
    Import = 1,
    /// <summary>Calculé à la clôture d'un exercice.</summary>
    Calcul = 2
}

/// <summary>
/// Actions tracées dans le journal d'audit. Le socle stocke l'action en texte
/// (AuditLog.Action) : utiliser <c>ActionAudit.X.ToString().ToUpperInvariant()</c>.
/// </summary>
public enum ActionAudit : byte
{
    Creation = 1,
    Modification = 2,
    SuppressionLogique = 3,
    Validation = 4,
    Rejet = 5,
    Export = 6,
    Cloture = 7,
    AttributionDroit = 8
}
