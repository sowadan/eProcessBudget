using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace eProcessBudget.Infrastructure.Data;

/// <summary>
/// Fabrique utilisée uniquement par les outils <c>dotnet ef</c> (migrations, scripts).
///
/// Chaîne de connexion utilisée, par ordre de priorité :
///   1. l'option <c>--connection</c> de <c>dotnet ef database update</c> (gérée par EF) ;
///   2. la variable d'environnement EPB_CONNEXION_DESIGN ;
///   3. ConnectionStrings:DefaultConnection de eProcessBudget.Web (appsettings.json, puis
///      appsettings.{ASPNETCORE_ENVIRONMENT ou Development}.json, puis user-secrets en
///      développement) : la même base que l'application.
/// </summary>
public class AppDbContextDesignFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>UserSecretsId de eProcessBudget.Web.csproj (à garder identique).</summary>
    private const string IdSecretsProjetWeb = "eProcessBudget-web-c3ed7360-8a0a-405c-99a0-8ab259a66ff3";

    public AppDbContext CreateDbContext(string[] args)
    {
        var chaine = Environment.GetEnvironmentVariable("EPB_CONNEXION_DESIGN") ?? LireChaineDuProjetWeb();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(chaine, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name))
            .Options;

        return new AppDbContext(options);
    }

    private static string LireChaineDuProjetWeb()
    {
        var dossierWeb = TrouverDossierWeb()
            ?? throw new InvalidOperationException(
                "Projet eProcessBudget.Web introuvable : lancez dotnet ef depuis la solution, " +
                "ou définissez la variable d'environnement EPB_CONNEXION_DESIGN.");

        var environnement = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var builder = new ConfigurationBuilder()
            .SetBasePath(dossierWeb)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environnement}.json", optional: true);

        // Comme l'application : en développement, la chaîne du poste vient de dotnet user-secrets.
        if (environnement == "Development") builder.AddUserSecrets(IdSecretsProjetWeb);

        var configuration = builder.AddEnvironmentVariables().Build();

        var chaine = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(chaine))
            throw new InvalidOperationException(
                $"ConnectionStrings:DefaultConnection est vide dans {dossierWeb}\\appsettings*.json.");

        return chaine;
    }

    /// <summary>Remonte l'arborescence depuis le dossier courant jusqu'à trouver eProcessBudget.Web.</summary>
    private static string? TrouverDossierWeb()
    {
        for (var dossier = new DirectoryInfo(Directory.GetCurrentDirectory()); dossier is not null; dossier = dossier.Parent)
        {
            var candidat = Path.Combine(dossier.FullName, "eProcessBudget.Web");
            if (File.Exists(Path.Combine(candidat, "appsettings.json"))) return candidat;
        }
        return null;
    }
}
