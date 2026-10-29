using eProcessBudget.Core.Entities;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Donne accès à l'utilisateur connecté depuis n'importe quel composant ou service.
///
/// Injecté en Scoped : une instance par circuit Blazor Server. L'identité vient du cookie
/// d'authentification (via <see cref="IPrincipalCourant"/>) ; le compte est relu en base une
/// fois, puis mis en cache tant que l'identité du circuit ne change pas.
///
/// C'est le SEUL endroit qui sait comment l'utilisateur courant est identifié : écrans et
/// services n'en dépendent qu'au travers de cette interface.
/// </summary>
public interface IUserContextService
{
    /// <summary>Identifiant de l'utilisateur connecté. Ulid.Empty s'il est anonyme ou désactivé.</summary>
    Task<Ulid> ObtenirUserIdAsync();

    Task<string> ObtenirMatriculeAsync();
    Task<string> ObtenirNomAsync();
    Task<Utilisateur?> ObtenirUtilisateurAsync();

    /// <summary>
    /// Organisme de l'utilisateur connecté (cahier technique § 2.3). Ulid.Empty pour un
    /// Administrateur plateforme, un anonyme ou un compte désactivé.
    /// </summary>
    Task<Ulid> ObtenirOrganismeIdAsync();

    /// <summary>true pour un Administrateur plateforme actif.</summary>
    Task<bool> EstAdministrateurPlateformeAsync();
}

public class UserContextService : IUserContextService
{
    private readonly IPrincipalCourant _principal;
    private readonly IUtilisateurStore _store;

    // Chargement mémorisé par identité : plusieurs composants d'une même page interrogent
    // le service en parallèle, une seule lecture en base suffit.
    private Ulid _idCharge;
    private Task<Utilisateur?>? _chargement;

    public UserContextService(IPrincipalCourant principal, IUtilisateurStore store)
    {
        _principal = principal;
        _store = store;
    }

    public async Task<Ulid> ObtenirUserIdAsync()
    {
        var u = await ObtenirUtilisateurAsync();
        // Un compte désactivé n'a aucun droit : il est traité comme anonyme.
        return u is { EstActif: true } ? u.Id : Ulid.Empty;
    }

    public async Task<string> ObtenirMatriculeAsync()
        => (await ObtenirUtilisateurAsync())?.Matricule ?? string.Empty;

    public async Task<string> ObtenirNomAsync()
        => (await ObtenirUtilisateurAsync())?.NomPrenoms ?? string.Empty;

    public async Task<Ulid> ObtenirOrganismeIdAsync()
    {
        var u = await ObtenirUtilisateurAsync();
        return u is { EstActif: true, OrganismeId: { } organismeId } ? organismeId : Ulid.Empty;
    }

    public async Task<bool> EstAdministrateurPlateformeAsync()
        => await ObtenirUtilisateurAsync() is { EstActif: true, EstAdministrateurPlateforme: true };

    public async Task<Utilisateur?> ObtenirUtilisateurAsync()
    {
        // L'identité est relue à chaque appel (lecture en mémoire) : si la session est révoquée
        // pendant la vie du circuit, l'utilisateur redevient anonyme immédiatement.
        var id = (await _principal.ObtenirAsync()).UtilisateurId();
        if (id == Ulid.Empty) return null;

        if (_chargement is null || _idCharge != id)
        {
            _idCharge = id;
            _chargement = _store.TrouverCompteAsync(id);
        }

        return await _chargement;
    }
}
