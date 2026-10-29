using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Identité authentifiée de la requête ou du circuit en cours, telle que portée par le
/// cookie. Ne lit pas la base : c'est le maillon le plus bas, utilisé par la fabrique de
/// contexte (organisme courant) et par IUserContextService.
/// </summary>
public interface IPrincipalCourant
{
    Task<ClaimsPrincipal> ObtenirAsync();
}

public sealed class PrincipalCourant : IPrincipalCourant
{
    private static readonly ClaimsPrincipal Anonyme = new(new ClaimsIdentity());

    private readonly AuthenticationStateProvider _etat;
    private readonly IHttpContextAccessor _http;

    public PrincipalCourant(AuthenticationStateProvider etat, IHttpContextAccessor http)
    {
        _etat = etat;
        _http = http;
    }

    public async Task<ClaimsPrincipal> ObtenirAsync()
    {
        try
        {
            // Composant Blazor (rendu statique ou circuit) : l'état est fourni par le framework
            // et suit les révocations de session faites pendant la vie du circuit.
            return (await _etat.GetAuthenticationStateAsync()).User;
        }
        catch (InvalidOperationException)
        {
            // Hors composant (point de terminaison HTTP, traitement planifié) : l'état n'est
            // pas initialisé. On lit alors la requête, s'il y en a une.
            return _http.HttpContext?.User ?? Anonyme;
        }
    }
}
