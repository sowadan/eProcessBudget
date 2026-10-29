using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>Ligne du journal des connexions, telle qu'affichée.</summary>
public sealed record LigneJournalConnexion(
    DateTime DateHeure, string Login, string? NomPrenoms, string AdresseIp, string? UserAgent,
    StatutConnexion Statut, string? Motif);

/// <summary>Critères de recherche dans le journal des connexions.</summary>
public sealed record FiltreJournalConnexion(
    string? Login = null, StatutConnexion? Statut = null, DateTime? Du = null, DateTime? Au = null,
    int Page = 1, int Taille = 50);

/// <summary>
/// Consultation du journal des connexions (auth.JournalConnexion). Le journal est en ajout
/// seul : ce service n'expose ni modification ni suppression (§ 2.7).
/// </summary>
public interface IJournalConnexionService
{
    Task<(List<LigneJournalConnexion> Items, int Total)> RechercherAsync(FiltreJournalConnexion filtre);
}

/// <summary>
/// Cloisonnement : un utilisateur d'organisme ne voit que les connexions de son organisme ;
/// un Administrateur plateforme voit les connexions des comptes plateforme et les tentatives
/// sur des identifiants inconnus (aucun organisme), pas celles des organismes.
/// </summary>
public class JournalConnexionService : IJournalConnexionService
{
    private const int TailleMax = 200;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IDroitService _droits;

    public JournalConnexionService(
        IDbContextFactory<AppDbContext> factory,
        IUserContextService utilisateurCourant,
        IDroitService droits)
    {
        _factory = factory;
        _utilisateurCourant = utilisateurCourant;
        _droits = droits;
    }

    public async Task<(List<LigneJournalConnexion> Items, int Total)> RechercherAsync(FiltreJournalConnexion filtre)
    {
        var userId = await _utilisateurCourant.ObtenirUserIdAsync();
        if (!await _droits.ADroitAsync(userId, TypeDroit.AuditConsultation)) return ([], 0);

        var organismeId = await _utilisateurCourant.ObtenirOrganismeIdAsync();
        Ulid? perimetre = organismeId == Ulid.Empty ? null : organismeId;

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.JournalConnexions.AsNoTracking().Where(j => j.OrganismeId == perimetre);

        if (!string.IsNullOrWhiteSpace(filtre.Login))
        {
            var login = filtre.Login.Trim();
            q = q.Where(j => j.Login.Contains(login));
        }
        if (filtre.Statut.HasValue) q = q.Where(j => j.Statut == filtre.Statut.Value);
        if (filtre.Du.HasValue)     q = q.Where(j => j.DateHeure >= filtre.Du.Value);
        if (filtre.Au.HasValue)     q = q.Where(j => j.DateHeure <= filtre.Au.Value);

        var total = await q.CountAsync();
        var taille = Math.Clamp(filtre.Taille, 1, TailleMax);

        var items = await q.OrderByDescending(j => j.DateHeure)
            .Skip(Math.Max(0, (filtre.Page - 1) * taille))
            .Take(taille)
            .Select(j => new LigneJournalConnexion(
                j.DateHeure,
                j.Login,
                ctx.Utilisateurs.Where(u => u.Id == j.UtilisateurId).Select(u => u.NomPrenoms).FirstOrDefault(),
                j.AdresseIp,
                j.UserAgent,
                j.Statut,
                j.Motif))
            .ToListAsync();

        return (items, total);
    }
}
