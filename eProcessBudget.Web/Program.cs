using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Web.Features.Parametrage;
using eProcessBudget.Web.Features.Rapports;
using eProcessBudget.Web.Securite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MudBlazor.Services;

// ═══════════════════════════════════════════════════════════════════════════════
//  Démarrage de l'application.
//
//  Ce fichier reste court volontairement : tout le câblage du socle est dans
//  eProcessBudget.Infrastructure/ServiceCollectionExtensions.cs, la sécurité de l'hôte
//  (cookie, autorisation, limitation de débit, en-têtes) dans Securite/ConfigurationSecurite.cs.
//  Ici on ne trouve que ce qui est propre à l'hôte web — Blazor, pipeline HTTP.
//
//  Pour enregistrer VOS services métier, ajoutez un appel à votre propre méthode
//  d'extension juste après AddSocleApplicatif.
// ═══════════════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// Nécessaire pour servir les ressources statiques de MudBlazor en développement.
builder.WebHost.UseStaticWebAssets();

// ── Blazor Server ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── MudBlazor ────────────────────────────────────────────────────────────────
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass        = MudBlazor.Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates    = false;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
    config.SnackbarConfiguration.ShowTransitionDuration = 200;
    config.SnackbarConfiguration.HideTransitionDuration = 200;
});

// ── Socle applicatif : options, stockage, droits, notifications ─────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddSocleApplicatif(builder.Configuration);

// ── Authentification par cookie, accès authentifié par défaut ───────────────
builder.Services.AddSecuriteWeb(builder.Configuration, builder.Environment);

// ── VOS SERVICES MÉTIER ──────────────────────────────────────────────────────
// builder.Services.AddServicesMetier(builder.Configuration);

builder.Services.AddAntiforgery();

var app = builder.Build();

// ── Pipeline HTTP ────────────────────────────────────────────────────────────
// DetailedErrors = true (appsettings ou variable ASPNETCORE_DETAILEDERRORS) affiche la pile
// d'exception complète hors développement. Réservé au diagnostic : à remettre à false ensuite.
var erreursDetaillees = app.Configuration.GetValue<bool>(WebHostDefaults.DetailedErrorsKey);

if (app.Environment.IsDevelopment() || erreursDetaillees)
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/erreur", createScopeForErrors: true);
}

if (!app.Environment.IsDevelopment()) app.UseHsts();

// On n'active la redirection HTTPS que si un point d'écoute HTTPS existe réellement,
// sinon le middleware journalise « Failed to determine the https port » à chaque requête.
var urls = app.Configuration["urls"] ?? app.Configuration["ASPNETCORE_URLS"] ?? string.Empty;
var httpsConfigure =
       urls.Contains("https", StringComparison.OrdinalIgnoreCase)
    || !string.IsNullOrEmpty(app.Configuration["ASPNETCORE_HTTPS_PORTS"])
    || !string.IsNullOrEmpty(app.Configuration["https_port"]);

if (httpsConfigure) app.UseHttpsRedirection();

app.UseEnTetesSecurite();
app.UseStaticFiles();

app.UseRateLimiter();
// Adresse IP et identifiant de requête, pour le journal d'audit des requêtes HTTP (hors circuit).
app.Use(async (http, suivant) =>
{
    http.RequestServices.GetRequiredService<eProcessBudget.Infrastructure.Services.Audit.IContexteConnexion>()
        .Definir(http.Connection.RemoteIpAddress?.ToString(), http.TraceIdentifier);
    await suivant();
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Feuilles de style, scripts et images : servis aussi à la page de connexion.
app.MapStaticAssets().AllowAnonymous();
app.MapPointsTerminaisonCompte();
app.MapPointsTerminaisonParametrage();
app.MapPointsTerminaisonRapports();
app.MapRazorComponents<eProcessBudget.Web.Components.App>()
   .AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = ConfigurationSecurite.FrameAncestors);

// ── Contrôles de configuration hors développement ───────────────────────────
if (!app.Environment.IsDevelopment())
{
    var hotes = app.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(hotes) || hotes.Split(';').Any(h => h.Trim() == "*"))
        app.Logger.LogWarning(
            "AllowedHosts vaut « * » : tout en-tête Host est accepté. Restreignez-le aux noms réels " +
            "du site (appsettings.Production.json ou variable AllowedHosts, voir docs/10-deploiement.md).");
}

// ── Initialisation de la base (migrations + données de référence) ────────────
await InitialiserBaseAsync(app);

app.Run();

// ═══════════════════════════════════════════════════════════════════════════════
/// <summary>
/// Applique les migrations et insère les données de référence au démarrage.
/// Ne fait rien si Persistance:MigrerAuDemarrage est à false (cas de la production, où la
/// migration est pilotée par script).
/// </summary>
static async Task InitialiserBaseAsync(WebApplication app)
{
    var persistance = app.Services.GetRequiredService<IOptions<PersistanceOptions>>().Value;
    if (!persistance.MigrerAuDemarrage) return;

    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Demarrage");

    try
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Fabrique sans organisme : le seed travaille au niveau de la plateforme.
        var factory = services.GetRequiredKeyedService<IDbContextFactory<AppDbContext>>(
            FabriqueContexteOrganisme.CleSansOrganisme);

        await using var db = await factory.CreateDbContextAsync();

        await db.Database.MigrateAsync();

        await SeedService.InitialiserAsync(db,
            services.GetRequiredService<IOptions<AmorcageOptions>>().Value,
            services.GetRequiredService<IOptions<SecuriteOptions>>().Value,
            services.GetRequiredService<IPasswordHasher<Utilisateur>>(),
            logger);

        logger.LogInformation("Base de données prête (migrations et données de référence appliquées).");
    }
    catch (Exception ex)
    {
        // On journalise clairement plutôt que de laisser une pile d'exception opaque :
        // c'est presque toujours une chaîne de connexion ou des droits SQL.
        logger.LogCritical(ex,
            "Initialisation de la base impossible. Vérifiez ConnectionStrings:DefaultConnection " +
            "et les droits du compte SQL.");
        throw;
    }
}

/// <summary>Rendu public pour que Routes.razor puisse référencer l'assembly via typeof(Program).</summary>
public partial class Program { }


