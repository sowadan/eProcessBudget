using eProcessBudget.Core.Entities;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Accès en lecture au référentiel des utilisateurs, SANS contrôle de droits ni de
/// périmètre : réservé aux services de sécurité (utilisateur courant, notifications).
/// Les écrans passent par <see cref="IUtilisateurService"/>, qui applique ces contrôles.
/// Implémentation : UtilisateurStoreEf (table auth.Utilisateur).
/// </summary>
public interface IUtilisateurStore
{
    /// <summary>Recherche par identifiant de connexion OU par email. null si inconnu.</summary>
    Task<Utilisateur?> TrouverParIdentifiantAsync(string identifiant);

    /// <summary>Compte avec ses profils attribués.</summary>
    Task<Utilisateur?> TrouverParIdAsync(Ulid id);

    /// <summary>Compte seul, sans relation : lecture légère pour l'utilisateur courant.</summary>
    Task<Utilisateur?> TrouverCompteAsync(Ulid id);
}
