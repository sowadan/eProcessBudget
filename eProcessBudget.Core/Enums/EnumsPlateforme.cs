namespace eProcessBudget.Core.Enums;

// ═══════════════════════════════════════════════════════════════════════════════
//  Énumérations métier (cahier technique § 3.6) - plateforme et habilitations.
//
//  Stockées en TINYINT, contrôlées en base par une contrainte CHECK générée
//  automatiquement (AppDbContext). Les valeurs sont PERSISTÉES : ne jamais renuméroter.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>État d'un organisme sur la plateforme.</summary>
public enum StatutOrganisme : byte
{
    Actif = 1,
    Suspendu = 2,
    Archive = 3
}

/// <summary>Nature du périmètre couvert par une habilitation.</summary>
public enum TypePerimetre : byte
{
    OrganismeEntier = 1,
    Axe = 2,
    Programme = 3,
    Action = 4,
    Site = 5,
    Structure = 6
}

public enum NiveauAcces : byte
{
    Lecture = 1,
    Saisie = 2
}

/// <summary>
/// Circuit de double validation d'une habilitation : service financier puis
/// administrateur SI (deux personnes distinctes), dans un ordre quelconque.
/// </summary>
public enum StatutHabilitation : byte
{
    Demandee = 1,
    ValideeFinancier = 2,
    ValideeAdminSI = 3,
    Active = 4,
    Rejetee = 5,
    Revoquee = 6
}

public enum StatutConnexion : byte
{
    Succes = 1,
    Echec = 2,
    CompteBloque = 3,
    Deconnexion = 4
}
