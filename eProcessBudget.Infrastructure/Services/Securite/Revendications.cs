using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using eProcessBudget.Core.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Revendications (claims) portées par le cookie d'authentification, et lecture typée.
///
/// Le cookie ne transporte que des identifiants : les droits n'y figurent JAMAIS (ils sont
/// recalculés par IDroitService), pour qu'un retrait de droit prenne effet sans attendre
/// l'expiration de la session.
/// </summary>
public static class Revendications
{
    /// <summary>Organisme de rattachement (absent pour un Administrateur plateforme).</summary>
    public const string Organisme = "epb:organisme";

    /// <summary>Présente (valeur « 1 ») pour un Administrateur plateforme.</summary>
    public const string Plateforme = "epb:plateforme";

    /// <summary>
    /// Empreinte de sécurité du compte au moment de la connexion. Elle change avec le mot de
    /// passe, l'organisme ou le niveau du compte : toute session ouverte avant ce changement
    /// est alors refusée (voir IAuthentificationService.ValiderSessionAsync).
    /// </summary>
    public const string Empreinte = "epb:empreinte";

    public static Ulid UtilisateurId(this ClaimsPrincipal principal)
        => Ulid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Ulid.Empty;

    /// <summary>Organisme de l'utilisateur authentifié ; Ulid.Empty pour un compte plateforme ou un anonyme.</summary>
    public static Ulid OrganismeId(this ClaimsPrincipal principal)
        => Ulid.TryParse(principal.FindFirstValue(Organisme), out var id) ? id : Ulid.Empty;

    /// <summary>Construit l'identité stockée dans le cookie, à partir du compte qui vient de s'authentifier.</summary>
    internal static ClaimsPrincipal CreerPrincipal(Utilisateur u)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.NomPrenoms),
            new(Empreinte, CalculerEmpreinte(u.Id, u.PasswordHash, u.OrganismeId, u.EstAdministrateurPlateforme)),
        };

        if (u.OrganismeId is { } organismeId) claims.Add(new Claim(Organisme, organismeId.ToString()));
        if (u.EstAdministrateurPlateforme) claims.Add(new Claim(Plateforme, "1"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>
    /// SHA-256 (tronqué) des éléments qui conditionnent la validité d'une session. Ne révèle
    /// rien du mot de passe : l'empreinte BCrypt est elle-même salée.
    /// </summary>
    internal static string CalculerEmpreinte(Ulid id, string? passwordHash, Ulid? organismeId, bool estAdministrateurPlateforme)
    {
        var source = $"{id}|{passwordHash}|{organismeId}|{(estAdministrateurPlateforme ? 1 : 0)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)), 0, 16);
    }
}
