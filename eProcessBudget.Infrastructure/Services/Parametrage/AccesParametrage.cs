using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Securite;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>
/// Contrôle d'accès commun aux services du Paramétrage (cahier technique § 4.2) :
/// ParametrageLire pour consulter, ParametrageGerer pour modifier, toujours depuis un
/// compte d'organisme (le contexte de données est alors restreint à cet organisme).
/// </summary>
public sealed class AccesParametrage
{
    public const string ModuleAudit = "Parametrage";
    public const string MessageLecture = "Vous n'avez pas le droit de consulter le paramétrage.";
    public const string MessageGestion = "Vous n'avez pas le droit de modifier le paramétrage.";

    private readonly IUserContextService _utilisateurCourant;
    private readonly IDroitService _droits;

    public AccesParametrage(IUserContextService utilisateurCourant, IDroitService droits)
    {
        _utilisateurCourant = utilisateurCourant;
        _droits = droits;
    }

    /// <summary>Utilisateur connecté s'il peut consulter, sinon Ulid.Empty.</summary>
    public Task<Ulid> LecteurAsync() => ActeurAsync(TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer);

    /// <summary>Utilisateur connecté s'il peut modifier, sinon Ulid.Empty.</summary>
    public Task<Ulid> GestionnaireAsync() => ActeurAsync(TypeDroit.ParametrageGerer);

    /// <summary>
    /// Utilisateur d'organisme connecté, sans exigence de droit : les listes de choix des
    /// référentiels servent à tous les modules de saisie.
    /// </summary>
    public async Task<Ulid> MembreOrganismeAsync()
        => await _utilisateurCourant.ObtenirOrganismeIdAsync() == Ulid.Empty
            ? Ulid.Empty
            : await _utilisateurCourant.ObtenirUserIdAsync();

    /// <summary>Utilisateur d'organisme connecté s'il détient l'un des droits, sinon Ulid.Empty.</summary>
    public async Task<Ulid> ActeurAsync(params TypeDroit[] droits)
    {
        if (await _utilisateurCourant.ObtenirOrganismeIdAsync() == Ulid.Empty) return Ulid.Empty;
        var userId = await _utilisateurCourant.ObtenirUserIdAsync();
        return await _droits.ADroitAsync(userId, droits) ? userId : Ulid.Empty;
    }
}
