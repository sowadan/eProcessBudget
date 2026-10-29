using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Options;

namespace eProcessBudget.Web.Securite;

/// <summary>
/// État d'authentification d'un circuit Blazor, revérifié à intervalle régulier.
///
/// Un circuit (connexion SignalR) peut vivre des heures sans nouvelle requête HTTP : le
/// cookie n'est alors plus relu. Cette classe repose la question en base toutes les
/// <see cref="SecuriteOptions.IntervalleRevalidationSecondes"/> secondes ; si le compte a été
/// désactivé, bloqué, a changé de mot de passe ou si son organisme est suspendu, la session
/// devient anonyme et l'utilisateur est renvoyé vers la page de connexion.
/// </summary>
public sealed class EtatAuthentificationRevalide : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeSpan _intervalle;

    public EtatAuthentificationRevalide(
        ILoggerFactory loggerFactory, IServiceScopeFactory scopes, IOptions<SecuriteOptions> options)
        : base(loggerFactory)
    {
        _scopes = scopes;
        _intervalle = TimeSpan.FromSeconds(Math.Max(15, options.Value.IntervalleRevalidationSecondes));
    }

    protected override TimeSpan RevalidationInterval => _intervalle;

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        if (authenticationState.User.Identity?.IsAuthenticated != true) return true;

        // Portée dédiée : la vérification tourne en tâche de fond, hors du contexte du circuit.
        await using var scope = _scopes.CreateAsyncScope();
        var authentification = scope.ServiceProvider.GetRequiredService<IAuthentificationService>();
        return await authentification.ValiderSessionAsync(authenticationState.User);
    }
}
