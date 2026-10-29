using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Référentiel utilisateurs adossé à la table auth.Utilisateur via Entity Framework Core.
/// </summary>
public class UtilisateurStoreEf : IUtilisateurStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    // Fabrique sans organisme : la table auth.Utilisateur n'est pas cloisonnée par le filtre
    // global, et ce store sert à résoudre l'utilisateur courant lui-même.
    public UtilisateurStoreEf([FromKeyedServices(FabriqueContexteOrganisme.CleSansOrganisme)] IDbContextFactory<AppDbContext> factory)
        => _factory = factory;

    public async Task<Utilisateur?> TrouverParIdentifiantAsync(string identifiant)
    {
        if (string.IsNullOrWhiteSpace(identifiant)) return null;
        var cle = identifiant.Trim();

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.Matricule == cle || (u.Email != "" && u.Email == cle))
            // L'identifiant de connexion l'emporte sur l'email d'un autre compte.
            .OrderBy(u => u.Matricule == cle ? 0 : 1)
            .FirstOrDefaultAsync();
    }

    public async Task<Utilisateur?> TrouverParIdAsync(Ulid id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Utilisateurs
            .Include(u => u.UtilisateurProfils).ThenInclude(up => up.Profil)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<Utilisateur?> TrouverCompteAsync(Ulid id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }
}
