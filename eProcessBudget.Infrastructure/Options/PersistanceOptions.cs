namespace eProcessBudget.Infrastructure.Options;

/// <summary>
/// Réglages de la base de données, section "Persistance" de appsettings.json.
/// L'application fonctionne uniquement sur SQL Server (ConnectionStrings:DefaultConnection).
/// Voir docs/09-base-de-donnees.md.
/// </summary>
public class PersistanceOptions
{
    public const string SectionName = "Persistance";

    /// <summary>
    /// Applique les migrations EF Core au démarrage et insère les données de référence
    /// (profils système, paramètres de notification, premier utilisateur).
    /// À laisser à true en développement, à false en production (migration pilotée par script).
    /// </summary>
    public bool MigrerAuDemarrage { get; set; } = true;
}
