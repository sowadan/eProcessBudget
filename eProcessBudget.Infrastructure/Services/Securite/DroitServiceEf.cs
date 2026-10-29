using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Droits calculés depuis la base : profils attribués + droits directs, moins les droits
/// explicitement retirés, bornés par le niveau du compte (DroitHelper.FiltrerSelonNiveau).
///
/// Le résultat est mis en cache 2 minutes par utilisateur : la question « a-t-il ce droit ? »
/// est posée des dizaines de fois par rendu de page (chaque entrée de menu, chaque bouton).
/// Après avoir modifié les profils d'un utilisateur, appelez <see cref="InvaliderCache"/>.
/// </summary>
public class DroitServiceEf : IDroitService
{
    private static readonly TimeSpan DureeCache = TimeSpan.FromMinutes(2);

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IMemoryCache _cache;

    // Fabrique sans organisme : profils et droits sont globaux, et le calcul doit pouvoir
    // porter sur un autre utilisateur que l'utilisateur connecté (destinataires d'une alerte).
    public DroitServiceEf(
        [FromKeyedServices(FabriqueContexteOrganisme.CleSansOrganisme)] IDbContextFactory<AppDbContext> factory,
        IMemoryCache cache)
    {
        _factory = factory;
        _cache = cache;
    }

    public async Task<IReadOnlySet<TypeDroit>> ObtenirDroitsAsync(Ulid utilisateurId)
    {
        if (utilisateurId == Ulid.Empty) return new HashSet<TypeDroit>();

        if (_cache.TryGetValue(CacheSecurite.CleDroits(utilisateurId), out IReadOnlySet<TypeDroit>? cache) && cache is not null)
            return cache;

        var maintenant = DateTime.UtcNow;
        await using var ctx = await _factory.CreateDbContextAsync();

        // 0. Le niveau du compte borne ses droits (cahier fonctionnel § 2.2) ; un compte
        //    désactivé n'en a aucun.
        var compte = await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.Id == utilisateurId)
            .Select(u => new { u.EstActif, u.EstAdministrateurPlateforme })
            .FirstOrDefaultAsync();
        if (compte is not { EstActif: true }) return new HashSet<TypeDroit>();

        // 1. Droits hérités des profils actifs et non expirés.
        var viaProfils = await ctx.UtilisateurProfils
            .Where(up => up.UtilisateurId == utilisateurId
                      && up.DateDebut <= maintenant
                      && (up.DateFin == null || up.DateFin >= maintenant)
                      && up.Profil.EstActif)
            .SelectMany(up => up.Profil.Droits.Select(d => d.TypeDroit))
            .Distinct()
            .ToListAsync();

        // 2. Droits directs, en vigueur, accordés ou retirés.
        var directs = await ctx.UtilisateurDroits
            .Where(ud => ud.UtilisateurId == utilisateurId
                      && ud.DateDebut <= maintenant
                      && (ud.DateFin == null || ud.DateFin >= maintenant))
            .Select(ud => new { ud.TypeDroit, ud.EstAccorde })
            .ToListAsync();

        var effectifs = viaProfils.ToHashSet();
        foreach (var d in directs.Where(d => d.EstAccorde)) effectifs.Add(d.TypeDroit);
        // Un retrait explicite gagne toujours sur un droit hérité d'un profil.
        foreach (var d in directs.Where(d => !d.EstAccorde)) effectifs.Remove(d.TypeDroit);

        // 3. Plateforme : administration de l'instance uniquement, aucun droit budgétaire.
        //    Organisme : jamais les droits qui touchent les données partagées.
        IReadOnlySet<TypeDroit> resultat =
            DroitHelper.FiltrerSelonNiveau(effectifs, compte.EstAdministrateurPlateforme).ToHashSet();
        _cache.Set(CacheSecurite.CleDroits(utilisateurId), resultat, DureeCache);
        return resultat;
    }

    public async Task<bool> ADroitAsync(Ulid utilisateurId, params TypeDroit[] droits)
    {
        if (droits.Length == 0) return true;
        var mes = await ObtenirDroitsAsync(utilisateurId);
        return droits.Any(mes.Contains);
    }

    public async Task<bool> ATousLesDroitsAsync(Ulid utilisateurId, params TypeDroit[] droits)
    {
        if (droits.Length == 0) return true;
        var mes = await ObtenirDroitsAsync(utilisateurId);
        return droits.All(mes.Contains);
    }

    public void InvaliderCache(Ulid utilisateurId) => CacheSecurite.Invalider(_cache, utilisateurId);
}


