using eProcessBudget.Core.Enums;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Ce que peut faire l'utilisateur courant.
///
/// Un seul point d'entrée pour toute l'application : menus, pages, boutons et endpoints
/// posent tous la même question à ce service. Ne dupliquez jamais la logique de droits
/// ailleurs - si une règle change, elle doit changer ici.
/// </summary>
public interface IDroitService
{
    /// <summary>Ensemble des droits effectifs (profils + droits directs - droits retirés).</summary>
    Task<IReadOnlySet<TypeDroit>> ObtenirDroitsAsync(Ulid utilisateurId);

    /// <summary>true si l'utilisateur possède AU MOINS UN des droits demandés.</summary>
    Task<bool> ADroitAsync(Ulid utilisateurId, params TypeDroit[] droits);

    /// <summary>true si l'utilisateur possède TOUS les droits demandés.</summary>
    Task<bool> ATousLesDroitsAsync(Ulid utilisateurId, params TypeDroit[] droits);

    /// <summary>
    /// Vide les caches de sécurité d'un utilisateur (droits, validité de session) après
    /// modification de ses profils, de son mot de passe ou de son état.
    /// </summary>
    void InvaliderCache(Ulid utilisateurId);
}


