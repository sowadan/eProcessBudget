using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eProcessBudget.Infrastructure.Data;

/// <summary>
/// Fabrique de contexte injectée sous <c>IDbContextFactory&lt;AppDbContext&gt;</c> : chaque
/// contexte créé reçoit l'organisme de l'utilisateur connecté (cahier technique § 2.3).
/// Les services métier n'ont donc rien à faire pour être cloisonnés :
///
///     await using var ctx = await _factory.CreateDbContextAsync();
///     var axes = await ctx.Axes.ToListAsync();   // uniquement les axes de l'organisme courant
///
/// Scoped : une instance par circuit, comme l'identité qu'elle lit. L'organisme vient du
/// cookie, sans lecture en base.
///
/// La fabrique d'origine reste disponible en service à clé <see cref="CleSansOrganisme"/>,
/// pour les rares usages légitimes sans organisme : authentification (avant qu'il y ait un
/// utilisateur), seed, traitements planifiés (qui parcourent les organismes un par un).
/// </summary>
public sealed class FabriqueContexteOrganisme : IDbContextFactory<AppDbContext>
{
    /// <summary>Clé du service donnant accès à la fabrique EF Core d'origine, sans organisme courant.</summary>
    public const string CleSansOrganisme = "sans-organisme";

    private readonly IDbContextFactory<AppDbContext> _origine;
    private readonly IPrincipalCourant _principal;
    private readonly IContexteConnexion _connexion;

    public FabriqueContexteOrganisme(
        [FromKeyedServices(CleSansOrganisme)] IDbContextFactory<AppDbContext> origine,
        IPrincipalCourant principal, IContexteConnexion connexion)
    {
        _origine = origine;
        _principal = principal;
        _connexion = connexion;
    }

    /// <summary>
    /// Non pris en charge : résoudre l'identité est asynchrone, et la bloquer gèlerait le
    /// circuit. Utilisez <see cref="CreateDbContextAsync"/>.
    /// </summary>
    public AppDbContext CreateDbContext()
        => throw new NotSupportedException(
            "Utilisez CreateDbContextAsync : l'organisme courant se résout de façon asynchrone.");

    public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var organismeId = (await _principal.ObtenirAsync()).OrganismeId();

        var ctx = await _origine.CreateDbContextAsync(cancellationToken);
        ctx.OrganismeCourantId = organismeId;
        // Repris par la journalisation automatique des écritures (§ 2.7).
        ctx.AdresseIp = _connexion.AdresseIp;
        ctx.CorrelationId = _connexion.CorrelationId;
        return ctx;
    }
}
