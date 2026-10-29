namespace eProcessBudget.Core.Enums;

// ═══════════════════════════════════════════════════════════════════════════════
//  Énumérations métier (cahier technique § 3.6) - budget-programme et performance.
//  Valeurs PERSISTÉES en TINYINT : ne jamais renuméroter.
// ═══════════════════════════════════════════════════════════════════════════════

public enum NiveauIndicateur : byte
{
    Programme = 1,
    Action = 2,
    /// <summary>Résultat immédiat (extrant), rattaché à une action.</summary>
    Extrant = 3
}

public enum SensEvolution : byte
{
    HausseAttendue = 1,
    BaisseAttendue = 2
}

/// <summary>Niveau d'un élément du cadre logique.</summary>
public enum TypeElementCL : byte
{
    ObjectifGeneral = 1,
    ObjectifSpecifique = 2,
    ResultatImmediat = 3
}

public enum TypeAnalyse : byte
{
    Contrainte = 1,
    PrioriteActionStrategique = 2,
    DefiMajeur = 3,
    Recommandation = 4,
    CommentaireRubrique = 5
}
