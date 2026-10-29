using System.Text.Json;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eProcessBudget.Infrastructure.Services.Audit;

/// <summary>
/// Journal d'audit persisté dans la table aud.JournalAudit.
/// </summary>
public class AuditServiceEf : IAuditService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IHttpContextAccessor _httpCtx;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IDroitService _droits;
    private readonly IContexteConnexion _connexion;
    private readonly ILogger<AuditServiceEf> _log;

    public AuditServiceEf(
        IDbContextFactory<AppDbContext> factory,
        IHttpContextAccessor httpCtx,
        IUserContextService utilisateurCourant,
        IDroitService droits,
        IContexteConnexion connexion,
        ILogger<AuditServiceEf> log)
    {
        _factory = factory;
        _httpCtx = httpCtx;
        _utilisateurCourant = utilisateurCourant;
        _droits = droits;
        _connexion = connexion;
        _log = log;
    }

    public async Task LoggerAsync(
        Ulid? utilisateurId, string? matricule, string action, string module, string entite,
        string? entiteId = null, object? ancienneValeur = null, object? nouvelleValeur = null,
        string? commentaire = null, bool estSucces = true, string? messageErreur = null,
        Ulid? organismeId = null)
    {
        try
        {
            // Acteur non précisé : c'est l'utilisateur connecté (cas de tous les écrans).
            if (organismeId is null || matricule is null)
            {
                var courant = await _utilisateurCourant.ObtenirUtilisateurAsync();
                if (courant is not null && (utilisateurId is null || utilisateurId == courant.Id))
                {
                    organismeId ??= courant.OrganismeId;
                    matricule ??= courant.Matricule;
                }
            }

            var ligne = new AuditLog
            {
                OrganismeId          = organismeId,
                UtilisateurId        = utilisateurId,
                UtilisateurMatricule = matricule,
                Action               = action,
                Module               = module,
                Entite               = entite,
                EntiteId             = entiteId,
                AncienneValeur       = Serialiser(ancienneValeur),
                NouvelleValeur       = Serialiser(nouvelleValeur),
                Commentaire          = commentaire,
                EstSucces            = estSucces,
                MessageErreur        = messageErreur,
                // Pendant un circuit Blazor, pas de requête HTTP : l'adresse relevée à son ouverture.
                AdresseIP            = _httpCtx.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? _connexion.AdresseIp,
                CorrelationId        = _httpCtx.HttpContext?.TraceIdentifier ?? _connexion.CorrelationId,
                UserAgent            = Tronquer(_httpCtx.HttpContext?.Request.Headers.UserAgent.ToString(), 400),
                DateAction           = DateTime.UtcNow
            };

            await using var ctx = await _factory.CreateDbContextAsync();
            ctx.AuditLogs.Add(ligne);
            await ctx.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Une panne d'audit ne doit pas annuler l'action métier déjà réalisée.
            _log.LogError(ex, "Écriture du journal d'audit impossible ({Module}/{Action}).", module, action);
        }
    }

    public async Task<(List<LigneAudit> Items, int Total)> RechercherAsync(
        string? module = null, string? action = null, Ulid? utilisateurId = null,
        DateTime? du = null, DateTime? au = null, int page = 1, int taille = 50)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var q = await JournalVisibleAsync(ctx);
        if (q is null) return ([], 0);

        if (!string.IsNullOrWhiteSpace(module)) q = q.Where(a => a.Module == module);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action.Contains(action));
        if (utilisateurId.HasValue) q = q.Where(a => a.UtilisateurId == utilisateurId);
        if (du.HasValue) q = q.Where(a => a.DateAction >= du.Value);
        if (au.HasValue) q = q.Where(a => a.DateAction <= au.Value);

        var total = await q.CountAsync();

        // Projection explicite : la liste n'affiche pas les colonnes JSON, inutile de les charger.
        var items = await q.OrderByDescending(a => a.DateAction)
            .Skip(Math.Max(0, (page - 1) * taille))
            .Take(Math.Clamp(taille, 1, 200))
            .Select(a => new LigneAudit(
                a.DateAction, a.UtilisateurId, a.UtilisateurMatricule,
                a.Module, a.Action, a.Entite, a.EntiteId, a.EstSucces))
            .ToListAsync();

        return (items, total);
    }

    public async Task<AuditLog?> ObtenirDetailAsync(Ulid id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var q = await JournalVisibleAsync(ctx);
        return q is null ? null : await q.FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<string>> ObtenirModulesAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var q = await JournalVisibleAsync(ctx);
        return q is null ? [] : await q.Select(a => a.Module).Distinct().OrderBy(m => m).ToListAsync();
    }

    /// <summary>
    /// Lignes que l'utilisateur connecté peut consulter (null sans le droit) : celles de son
    /// organisme, ou celles de niveau plateforme pour un Administrateur plateforme.
    /// </summary>
    private async Task<IQueryable<AuditLog>?> JournalVisibleAsync(AppDbContext ctx)
    {
        var userId = await _utilisateurCourant.ObtenirUserIdAsync();
        if (!await _droits.ADroitAsync(userId, TypeDroit.AuditConsultation)) return null;

        var organismeId = await _utilisateurCourant.ObtenirOrganismeIdAsync();
        Ulid? perimetre = organismeId == Ulid.Empty ? null : organismeId;

        return ctx.AuditLogs.AsNoTracking().Where(a => a.OrganismeId == perimetre);
    }

    private static string? Serialiser(object? valeur)
        => valeur is null ? null : JsonSerializer.Serialize(valeur);

    private static string? Tronquer(string? valeur, int max)
        => string.IsNullOrEmpty(valeur) || valeur.Length <= max ? valeur : valeur[..max];
}
