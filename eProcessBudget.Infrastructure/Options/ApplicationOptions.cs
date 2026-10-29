namespace eProcessBudget.Infrastructure.Options;

/// <summary>
/// Identité visuelle et textuelle de l'application, section "Application" de appsettings.json.
///
/// C'est le premier endroit à modifier quand vous démarrez un projet : le nom, le sigle et
/// les couleurs se propagent automatiquement au titre de l'onglet, à la barre latérale,
/// au thème MudBlazor et aux emails.
/// </summary>
public class ApplicationOptions
{
    public const string SectionName = "Application";

    /// <summary>Nom complet affiché dans le titre de l'onglet et les emails.</summary>
    public string Nom { get; set; } = "eProcessBudget";

    /// <summary>Nom court affiché dans la barre latérale (2 à 12 caractères).</summary>
    public string NomCourt { get; set; } = "eProcessBudget";

    /// <summary>Sigle de 1 à 3 lettres affiché dans le logo par défaut.</summary>
    public string Sigle { get; set; } = "EPB";

    /// <summary>Sous-titre affiché sous le nom court dans la barre latérale.</summary>
    public string Description { get; set; } = "Application interne";

    /// <summary>
    /// Icône carrée du logo, pour un fond clair (ex. "/img/logo-icone.svg"). Vide = pastille
    /// générée avec le sigle. Une image carrée est attendue : elle est affichée de 32 à 64 px.
    /// </summary>
    public string LogoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Variante de l'icône pour un fond sombre (barre latérale, page de connexion).
    /// Vide = <see cref="LogoUrl"/>.
    /// </summary>
    public string LogoFondSombreUrl { get; set; } = string.Empty;

    /// <summary>
    /// Met en couleur la première lettre du nom affiché à côté du logo (le « e » de
    /// eProcessBudget), comme sur le logotype.
    /// </summary>
    public bool AccentPremiereLettre { get; set; }

    public CouleursOptions Couleurs { get; set; } = new();
}

/// <summary>
/// Charte graphique. Ces valeurs sont injectées comme variables CSS
/// (--app-primaire, --app-secondaire, ...) dans la page et servent aussi à construire
/// le thème MudBlazor. Changer une couleur ici la change partout, sans toucher au code.
/// </summary>
public class CouleursOptions
{
    /// <summary>Actions principales, accents, état actif du menu.</summary>
    public string Primaire { get; set; } = "#C10A27";

    /// <summary>Barre latérale, en-têtes, texte de titre.</summary>
    public string Secondaire { get; set; } = "#01394F";

    /// <summary>Dégradés et variantes foncées.</summary>
    public string Tertiaire { get; set; } = "#540421";

    /// <summary>Mises en avant, avatars, pastilles.</summary>
    public string Accent { get; set; } = "#FFB928";
}


