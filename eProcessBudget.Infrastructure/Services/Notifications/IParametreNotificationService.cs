using System.Collections.Concurrent;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace eProcessBudget.Infrastructure.Services.Notifications;

/// <summary>Décision d'acheminement pour un type de notification donné.</summary>
public readonly record struct CanauxNotification(bool Interne, bool Email);

/// <summary>
/// Détermine, pour chaque type d'événement, s'il produit une notification interne
/// (cloche) et / ou un email. Réglable par un administrateur depuis
/// /administration/notifications.
/// </summary>
public interface IParametreNotificationService
{
    /// <summary>Chemin chaud : appelé à chaque création de notification, donc mis en cache.</summary>
    Task<CanauxNotification> ObtenirCanauxAsync(TypeNotification type);

    Task<List<ParametreNotification>> ObtenirTousAsync();

    /// <summary>
    /// Réglage commun à toute l'instance : réservé au détenteur de NotificationParametrage
    /// (Administrateur plateforme). false si l'utilisateur connecté n'a pas ce droit.
    /// </summary>
    Task<bool> DefinirCanauxAsync(TypeNotification type, bool interne, bool email, Ulid userId);
}

/// <summary>Paramétrage persisté dans la table ParametresNotification.</summary>
public class ParametreNotificationServiceEf : IParametreNotificationService
{
    private const string CachePrefixe = "param-notif:";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IMemoryCache _cache;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IDroitService _droits;

    public ParametreNotificationServiceEf(IDbContextFactory<AppDbContext> factory, IMemoryCache cache,
                                          IUserContextService utilisateurCourant, IDroitService droits)
    {
        _factory = factory;
        _cache = cache;
        _utilisateurCourant = utilisateurCourant;
        _droits = droits;
    }

    public async Task<CanauxNotification> ObtenirCanauxAsync(TypeNotification type)
    {
        if (_cache.TryGetValue(CachePrefixe + (int)type, out CanauxNotification cache))
            return cache;

        await using var ctx = await _factory.CreateDbContextAsync();
        var p = await ctx.ParametresNotification.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Type == type);

        // Défaut prudent si la ligne n'existe pas encore : interne oui, email non.
        var canaux = p is null
            ? new CanauxNotification(true, false)
            : new CanauxNotification(p.CanalInterneActif, p.CanalEmailActif);

        _cache.Set(CachePrefixe + (int)type, canaux, TimeSpan.FromMinutes(5));
        return canaux;
    }

    public async Task<List<ParametreNotification>> ObtenirTousAsync()
    {
        await GarantirLignesAsync();

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ParametresNotification.AsNoTracking()
            .OrderBy(p => p.Type)
            .ToListAsync();
    }

    public async Task<bool> DefinirCanauxAsync(TypeNotification type, bool interne, bool email, Ulid userId)
    {
        var acteurId = await _utilisateurCourant.ObtenirUserIdAsync();
        if (acteurId == Ulid.Empty || !await _droits.ADroitAsync(acteurId, TypeDroit.NotificationParametrage)) return false;

        await using var ctx = await _factory.CreateDbContextAsync();

        var p = await ctx.ParametresNotification.FirstOrDefaultAsync(x => x.Type == type);
        if (p is null)
        {
            p = new ParametreNotification { Type = type, CreePar = userId };
            ctx.ParametresNotification.Add(p);
        }

        p.CanalInterneActif = interne;
        p.CanalEmailActif = email;
        p.ModifiePar = userId;
        await ctx.SaveChangesAsync();

        _cache.Remove(CachePrefixe + (int)type);
        return true;
    }

    /// <summary>
    /// Crée les lignes manquantes. Idempotent : utile après l'ajout d'un nouveau
    /// TypeNotification dans l'énumération, sans nécessiter de migration de données.
    /// </summary>
    private async Task GarantirLignesAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var existants = await ctx.ParametresNotification.Select(p => p.Type).ToListAsync();
        var manquants = Enum.GetValues<TypeNotification>().Except(existants).ToList();
        if (manquants.Count == 0) return;

        foreach (var type in manquants)
        {
            ctx.ParametresNotification.Add(new ParametreNotification
            {
                Type              = type,
                CanalInterneActif = true,
                CanalEmailActif   = false
            });
        }
        await ctx.SaveChangesAsync();
    }
}

