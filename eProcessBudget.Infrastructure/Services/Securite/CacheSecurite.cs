using Microsoft.Extensions.Caching.Memory;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Clés des caches de sécurité par utilisateur (droits effectifs, validité de session).
/// Une modification du compte (profils, mot de passe, état) doit les vider ensemble, via
/// IDroitService.InvaliderCache, pour prendre effet immédiatement.
/// </summary>
internal static class CacheSecurite
{
    public static string CleDroits(Ulid utilisateurId) => $"droits:{utilisateurId}";
    public static string CleSession(Ulid utilisateurId) => $"session:{utilisateurId}";

    public static void Invalider(IMemoryCache cache, Ulid utilisateurId)
    {
        cache.Remove(CleDroits(utilisateurId));
        cache.Remove(CleSession(utilisateurId));
    }
}
