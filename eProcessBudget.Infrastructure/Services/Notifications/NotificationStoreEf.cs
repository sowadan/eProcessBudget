using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Notifications;

/// <summary>
/// Notifications persistées dans la table Notifications.
///
/// Toutes les requêtes sont filtrées par UtilisateurId : un utilisateur ne peut ni lire
/// ni supprimer la notification d'un autre, même en manipulant l'identifiant dans l'URL.
/// </summary>
public class NotificationStoreEf : INotificationStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public NotificationStoreEf(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<Notification> AjouterAsync(Notification notification)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        ctx.Notifications.Add(notification);
        await ctx.SaveChangesAsync();
        return notification;
    }

    public async Task<List<Notification>> AjouterLotAsync(IEnumerable<Notification> notifications)
    {
        var liste = notifications.ToList();
        if (liste.Count == 0) return liste;

        await using var ctx = await _factory.CreateDbContextAsync();
        ctx.Notifications.AddRange(liste);
        await ctx.SaveChangesAsync();   // peuple les Id
        return liste;
    }

    public async Task<int> CompterNonLuesAsync(Ulid utilisateurId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Notifications.CountAsync(n => n.UtilisateurId == utilisateurId && !n.EstLue);
    }

    public async Task<List<Notification>> ObtenirRecentesAsync(Ulid utilisateurId, int max = 8, bool uniquementNonLues = false)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Notifications.AsNoTracking()
            .Where(n => n.UtilisateurId == utilisateurId)
            .Where(n => !uniquementNonLues || !n.EstLue)
            .OrderByDescending(n => n.DateCreation)
            .Take(max)
            .ToListAsync();
    }

    public async Task<(List<Notification> Items, int Total, int NonLues)> ObtenirPageAsync(
        Ulid utilisateurId, int page = 1, int taille = 20,
        bool? estLue = null, TypeNotification? type = null, string? recherche = null)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var q = ctx.Notifications.AsNoTracking().Where(n => n.UtilisateurId == utilisateurId);

        if (estLue.HasValue) q = q.Where(n => n.EstLue == estLue.Value);
        if (type.HasValue) q = q.Where(n => n.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            q = q.Where(n => n.Message.Contains(r) || (n.Titre != null && n.Titre.Contains(r)));
        }

        var total = await q.CountAsync();
        var nonLues = await ctx.Notifications
            .CountAsync(n => n.UtilisateurId == utilisateurId && !n.EstLue);

        var items = await q.OrderByDescending(n => n.DateCreation)
            .Skip(Math.Max(0, (page - 1) * taille))
            .Take(taille)
            .ToListAsync();

        return (items, total, nonLues);
    }

    public async Task<int> MarquerLueAsync(Ulid id, Ulid utilisateurId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Notifications
            .Where(n => n.Id == id && n.UtilisateurId == utilisateurId && !n.EstLue)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.EstLue, true)
                .SetProperty(n => n.DateLecture, DateTime.UtcNow));
    }

    public async Task<int> MarquerNonLueAsync(Ulid id, Ulid utilisateurId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Notifications
            .Where(n => n.Id == id && n.UtilisateurId == utilisateurId && n.EstLue)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.EstLue, false)
                .SetProperty(n => n.DateLecture, (DateTime?)null));
    }

    public async Task<int> MarquerToutesLuesAsync(Ulid utilisateurId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Notifications
            .Where(n => n.UtilisateurId == utilisateurId && !n.EstLue)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.EstLue, true)
                .SetProperty(n => n.DateLecture, DateTime.UtcNow));
    }

    public async Task<int> SupprimerAsync(Ulid id, Ulid utilisateurId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        // Suppression physique : une notification lue n'a aucune valeur d'archive.
        return await ctx.Notifications
            .Where(n => n.Id == id && n.UtilisateurId == utilisateurId)
            .ExecuteDeleteAsync();
    }

    public async Task<int> SupprimerLuesAsync(Ulid utilisateurId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Notifications
            .Where(n => n.UtilisateurId == utilisateurId && n.EstLue)
            .ExecuteDeleteAsync();
    }
}


