namespace eProcessBudget.Infrastructure.Options;

/// <summary>
/// Compte d'amorçage, section "Amorcage" de appsettings.json : le premier Administrateur
/// plateforme, créé par le seed sur une base qui n'a encore aucun utilisateur.
///
/// Ce compte crée ensuite les organismes et leurs premiers administrateurs.
///
/// SÉCURITÉ : <see cref="MotDePasse"/> ne figure JAMAIS dans appsettings.json. Fournissez-le
/// par dotnet user-secrets en développement, par la variable d'environnement
/// Amorcage__MotDePasse en production, le temps du premier démarrage.
/// </summary>
public class AmorcageOptions
{
    public const string SectionName = "Amorcage";

    /// <summary>Identifiant de connexion du compte d'amorçage.</summary>
    public string Login { get; set; } = "admin";

    public string NomPrenoms { get; set; } = "Administrateur plateforme";
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Mot de passe initial. Sans lui, le compte n'est pas créé (ou, s'il existe sans mot de
    /// passe, n'en reçoit pas) : aucun compte sans mot de passe n'est connectable.
    /// </summary>
    public string? MotDePasse { get; set; }
}
