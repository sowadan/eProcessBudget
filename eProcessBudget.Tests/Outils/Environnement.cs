using System.Security.Claims;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace eProcessBudget.Tests.Outils;

/// <summary>
/// Base EF Core en mémoire, propre à chaque test, et fabriques des services à tester.
/// Les règles testées (blocage, double validation, cloisonnement) vivent dans les services
/// et le DbContext : elles ne dépendent pas de SQL Server.
/// </summary>
internal sealed class Environnement
{
    private readonly DbContextOptions<AppDbContext> _options =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"tests-{Guid.NewGuid()}").Options;

    /// <summary>Coût BCrypt minimal : le comportement est identique, les tests restent rapides.</summary>
    public HacheurMotDePasseBCrypt Hacheur { get; } = new(4);

    public SecuriteOptions Securite { get; } = new() { DureeBlocageMinutes = 15, NbTentativesMax = 5 };

    public UtilisateurCourantFactice UtilisateurCourant { get; } = new();

    public DroitsFactices Droits { get; } = new();

    /// <summary>Contexte sans organisme courant (niveau plateforme).</summary>
    public AppDbContext Contexte(Ulid? organismeCourant = null)
        => new(_options) { OrganismeCourantId = organismeCourant ?? Ulid.Empty };

    /// <summary>Fabrique dont les contextes portent l'organisme indiqué.</summary>
    public IDbContextFactory<AppDbContext> Fabrique(Ulid? organismeCourant = null)
        => new FabriqueTest(() => Contexte(organismeCourant));

    public AuthentificationService Authentification(IMemoryCache? cache = null)
        => new(Fabrique(), Hacheur, UtilisateurCourant, new AuditFactice(), new EmailQueue(),
               cache ?? new MemoryCache(new MemoryCacheOptions()), new EphemeralDataProtectionProvider(),
               Options.Create(Securite), Options.Create(new EmailOptions()), Options.Create(new ApplicationOptions()),
               NullLogger<AuthentificationService>.Instance);

    // ═════════════════════════════════════════════════════════════════════════

    public async Task<Ulid> CreerOrganismeAsync(string code, int nbTentativesMax = 5,
                                                StatutOrganisme statut = StatutOrganisme.Actif)
    {
        await using var ctx = Contexte();
        var organisme = new Organisme { Code = code, RaisonSociale = $"Organisme {code}", Statut = statut };
        ctx.Organismes.Add(organisme);
        ctx.ContournerFiltreOrganisme = true;
        ctx.ParametresOrganisme.Add(new ParametreOrganisme { OrganismeId = organisme.Id, NbTentativesMax = nbTentativesMax });
        await ctx.SaveChangesAsync();
        return organisme.Id;
    }

    public async Task<Utilisateur> CreerUtilisateurAsync(string login, Ulid? organismeId,
                                                         string? motDePasse = null, bool plateforme = false)
    {
        await using var ctx = Contexte();
        var u = new Utilisateur
        {
            Matricule = login,
            NomPrenoms = $"Utilisateur {login}",
            OrganismeId = organismeId,
            EstAdministrateurPlateforme = plateforme,
            EstActif = true
        };
        if (motDePasse is not null) u.PasswordHash = Hacheur.HashPassword(u, motDePasse);
        ctx.Utilisateurs.Add(u);
        await ctx.SaveChangesAsync();
        return u;
    }

    private sealed class FabriqueTest(Func<AppDbContext> creer) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => creer();
    }
}

/// <summary>Utilisateur connecté, choisi par le test.</summary>
internal sealed class UtilisateurCourantFactice : IUserContextService
{
    public Utilisateur? Utilisateur { get; set; }

    public Task<Ulid> ObtenirUserIdAsync() => Task.FromResult(Utilisateur is { EstActif: true } u ? u.Id : Ulid.Empty);
    public Task<string> ObtenirMatriculeAsync() => Task.FromResult(Utilisateur?.Matricule ?? "");
    public Task<string> ObtenirNomAsync() => Task.FromResult(Utilisateur?.NomPrenoms ?? "");
    public Task<Utilisateur?> ObtenirUtilisateurAsync() => Task.FromResult(Utilisateur);
    public Task<Ulid> ObtenirOrganismeIdAsync() => Task.FromResult(Utilisateur?.OrganismeId ?? Ulid.Empty);
    public Task<bool> EstAdministrateurPlateformeAsync() => Task.FromResult(Utilisateur?.EstAdministrateurPlateforme == true);
}

/// <summary>Droits attribués directement par le test, sans profils.</summary>
internal sealed class DroitsFactices : IDroitService
{
    private readonly Dictionary<Ulid, HashSet<TypeDroit>> _droits = new();

    public void Accorder(Ulid utilisateurId, params TypeDroit[] droits)
    {
        if (!_droits.TryGetValue(utilisateurId, out var ensemble)) _droits[utilisateurId] = ensemble = new();
        ensemble.UnionWith(droits);
    }

    public Task<IReadOnlySet<TypeDroit>> ObtenirDroitsAsync(Ulid utilisateurId)
        => Task.FromResult<IReadOnlySet<TypeDroit>>(_droits.GetValueOrDefault(utilisateurId) ?? new HashSet<TypeDroit>());

    public async Task<bool> ADroitAsync(Ulid utilisateurId, params TypeDroit[] droits)
        => droits.Any((await ObtenirDroitsAsync(utilisateurId)).Contains);

    public async Task<bool> ATousLesDroitsAsync(Ulid utilisateurId, params TypeDroit[] droits)
        => droits.All((await ObtenirDroitsAsync(utilisateurId)).Contains);

    public void InvaliderCache(Ulid utilisateurId) { }
}

internal sealed class AuditFactice : IAuditService
{
    public List<string> Actions { get; } = new();

    public Task LoggerAsync(Ulid? utilisateurId, string? matricule, string action, string module, string entite,
        string? entiteId = null, object? ancienneValeur = null, object? nouvelleValeur = null, string? commentaire = null,
        bool estSucces = true, string? messageErreur = null, Ulid? organismeId = null)
    {
        Actions.Add(action);
        return Task.CompletedTask;
    }

    public Task<(List<LigneAudit> Items, int Total)> RechercherAsync(string? module = null, string? action = null,
        Ulid? utilisateurId = null, DateTime? du = null, DateTime? au = null, int page = 1, int taille = 50)
        => Task.FromResult((new List<LigneAudit>(), 0));

    public Task<AuditLog?> ObtenirDetailAsync(Ulid id) => Task.FromResult<AuditLog?>(null);
    public Task<List<string>> ObtenirModulesAsync() => Task.FromResult(new List<string>());
}

/// <summary>Notifications non diffusées : les tests portent sur les règles ; les envois de groupe sont seulement relevés.</summary>
internal sealed class NotificationsFactices : INotificationService
{
    /// <summary>Envois de groupe : type, clé de l'objet métier, destinataires.</summary>
    public List<(TypeNotification Type, string? Cle, List<Ulid> Destinataires)> Groupes { get; } = new();

    public Task<Notification?> NotifierAsync(TypeNotification type, string message, Ulid destinataireId,
        string? titre = null, string? lien = null, string? cleEntite = null, Ulid? acteurId = null)
        => Task.FromResult<Notification?>(null);

    public Task NotifierGroupeAsync(TypeNotification type, string message, IEnumerable<Ulid> destinatairesIds,
        string? titre = null, string? lien = null, string? cleEntite = null, Ulid? acteurId = null)
    {
        Groupes.Add((type, cleEntite, destinatairesIds.ToList()));
        return Task.CompletedTask;
    }

    public Task<int> CompterNonLuesAsync(Ulid utilisateurId) => Task.FromResult(0);
    public Task<List<Notification>> ObtenirRecentesAsync(Ulid utilisateurId, int max = 8, bool uniquementNonLues = false)
        => Task.FromResult(new List<Notification>());
    public Task<(List<Notification> Items, int Total, int NonLues)> ObtenirPageAsync(Ulid utilisateurId, int page = 1,
        int taille = 20, bool? estLue = null, TypeNotification? type = null, string? recherche = null)
        => Task.FromResult((new List<Notification>(), 0, 0));
    public Task MarquerLueAsync(Ulid id, Ulid utilisateurId) => Task.CompletedTask;
    public Task MarquerNonLueAsync(Ulid id, Ulid utilisateurId) => Task.CompletedTask;
    public Task MarquerToutesLuesAsync(Ulid utilisateurId) => Task.CompletedTask;
    public Task<int> SupprimerAsync(Ulid id, Ulid utilisateurId) => Task.FromResult(0);
    public Task<int> SupprimerLuesAsync(Ulid utilisateurId) => Task.FromResult(0);
}

/// <summary>Identité fixée par le test, comme si elle venait du cookie.</summary>
internal sealed class PrincipalFactice(ClaimsPrincipal principal) : IPrincipalCourant
{
    public Task<ClaimsPrincipal> ObtenirAsync() => Task.FromResult(principal);
}
