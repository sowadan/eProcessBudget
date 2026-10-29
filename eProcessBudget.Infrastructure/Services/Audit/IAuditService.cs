using eProcessBudget.Core.Entities;

namespace eProcessBudget.Infrastructure.Services.Audit;

/// <summary>Ligne d'audit allégée pour l'affichage paginé (sans les colonnes JSON volumineuses).</summary>
public record LigneAudit(
    DateTime DateAction,
    Ulid? UtilisateurId,
    string? Matricule,
    string Module,
    string Action,
    string Entite,
    string? EntiteId,
    bool EstSucces);

/// <summary>
/// Journal de ce que font les utilisateurs. Appelez-le à chaque action qui modifie
/// des données ou qui touche à la sécurité.
///
/// Règle : l'audit ne doit JAMAIS faire échouer l'action métier. Une écriture d'audit
/// qui échoue est tracée dans les logs techniques, pas remontée à l'appelant.
///
/// Sans <c>organismeId</c> ni <c>matricule</c>, ce sont ceux de l'utilisateur connecté.
/// La consultation est cloisonnée : un utilisateur d'organisme ne voit que les lignes de
/// son organisme, un Administrateur plateforme que les lignes de niveau plateforme.
/// </summary>
public interface IAuditService
{
    Task LoggerAsync(
        Ulid? utilisateurId,
        string? matricule,
        string action,
        string module,
        string entite,
        string? entiteId = null,
        object? ancienneValeur = null,
        object? nouvelleValeur = null,
        string? commentaire = null,
        bool estSucces = true,
        string? messageErreur = null,
        Ulid? organismeId = null);

    Task<(List<LigneAudit> Items, int Total)> RechercherAsync(
        string? module = null, string? action = null, Ulid? utilisateurId = null,
        DateTime? du = null, DateTime? au = null, int page = 1, int taille = 50);

    Task<AuditLog?> ObtenirDetailAsync(Ulid id);

    Task<List<string>> ObtenirModulesAsync();
}


