namespace eProcessBudget.Infrastructure.Options;

/// <summary>
/// Relais SMTP utilisé pour le canal email des notifications, section "Email".
///
/// SÉCURITÉ : ne committez jamais d'identifiants réels ici. En production, fournissez
/// User et Password par variables d'environnement (Email__User, Email__Password) ou
/// par dotnet user-secrets en développement.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Interrupteur global. À false, aucun email n'est envoyé (les notifications internes fonctionnent toujours).</summary>
    public bool Active { get; set; }

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }

    /// <summary>Laisser vide pour un relais interne anonyme.</summary>
    public string? User { get; set; }
    public string? Password { get; set; }

    /// <summary>Adresse expéditrice du compte applicatif.</summary>
    public string ExpediteurApplicatif { get; set; } = "notifications@exemple.local";
    public string NomExpediteurApplicatif { get; set; } = "Notifications";

    /// <summary>
    /// URL publique de l'application. Sert à transformer les liens relatifs des notifications
    /// ("/budgets/12") en liens absolus cliquables dans les emails. Vide = pas de bouton.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public int DelaiReessaiSecondes { get; set; } = 30;
    public int NombreMaxTentatives { get; set; } = 3;
    public int TimeoutSecondes { get; set; } = 30;
}


