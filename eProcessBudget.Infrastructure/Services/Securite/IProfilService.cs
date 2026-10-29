using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Gestion des profils et de leur contenu, pour l'écran /administration/profils.
///
/// Un profil est un paquet de droits réutilisable. C'est par lui qu'on donne des droits
/// en pratique ; les droits directs sur un utilisateur restent l'exception.
///
/// Les profils sont GLOBAUX (cahier technique § 3.7) : les modifier touche tous les
/// organismes. Les écritures exigent donc le droit ProfilGestion, que seul un
/// Administrateur plateforme peut détenir (DroitHelper.ReservesPlateforme). Le service
/// contrôle lui-même les droits de l'utilisateur connecté.
/// </summary>
public interface IProfilService
{
    Task<List<Profil>> ListerAsync(bool actifsSeulement = false);
    Task<Profil?> ObtenirAsync(Ulid id);

    /// <summary>Droits actuellement portés par un profil.</summary>
    Task<IReadOnlySet<TypeDroit>> ObtenirDroitsAsync(Ulid profilId);

    Task<(bool Ok, string? Erreur, Profil? Profil)> CreerAsync(Profil profil);
    Task<(bool Ok, string? Erreur)> ModifierAsync(Profil profil);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid profilId);

    /// <summary>Remplace intégralement la liste des droits d'un profil.</summary>
    Task<(bool Ok, string? Erreur)> DefinirDroitsAsync(Ulid profilId, IEnumerable<TypeDroit> droits);
}

/// <summary>Profils persistés dans les tables Profils / ProfilDroits.</summary>
public class ProfilServiceEf : IProfilService
{
    private const string MessageDroitGestion =
        "Seul un Administrateur plateforme peut modifier les profils : ils sont partagés par tous les organismes.";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly Audit.IAuditService _audit;
    private readonly IDroitService _droits;
    private readonly IUserContextService _utilisateurCourant;

    public ProfilServiceEf(
        IDbContextFactory<AppDbContext> factory,
        Audit.IAuditService audit,
        IDroitService droits,
        IUserContextService utilisateurCourant)
    {
        _factory = factory;
        _audit = audit;
        _droits = droits;
        _utilisateurCourant = utilisateurCourant;
    }

    /// <summary>Utilisateur connecté s'il détient l'un des droits, sinon Ulid.Empty.</summary>
    private async Task<Ulid> ActeurAsync(params TypeDroit[] droits)
    {
        var userId = await _utilisateurCourant.ObtenirUserIdAsync();
        return await _droits.ADroitAsync(userId, droits) ? userId : Ulid.Empty;
    }

    public async Task<List<Profil>> ListerAsync(bool actifsSeulement = false)
    {
        // Lister sert aussi à attribuer des profils depuis la fiche d'un utilisateur.
        if (await ActeurAsync(TypeDroit.ProfilConsultation, TypeDroit.ProfilGestion, TypeDroit.ProfilAttribution) == Ulid.Empty)
            return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Profils.AsNoTracking().Include(p => p.Droits).AsQueryable();
        if (actifsSeulement) q = q.Where(p => p.EstActif);
        return await q.OrderBy(p => p.Libelle).ToListAsync();
    }

    public async Task<Profil?> ObtenirAsync(Ulid id)
    {
        if (await ActeurAsync(TypeDroit.ProfilConsultation, TypeDroit.ProfilGestion) == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Profils.AsNoTracking()
            .Include(p => p.Droits)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IReadOnlySet<TypeDroit>> ObtenirDroitsAsync(Ulid profilId)
    {
        if (await ActeurAsync(TypeDroit.ProfilConsultation, TypeDroit.ProfilGestion) == Ulid.Empty)
            return new HashSet<TypeDroit>();

        await using var ctx = await _factory.CreateDbContextAsync();
        var droits = await ctx.ProfilDroits.AsNoTracking()
            .Where(d => d.ProfilId == profilId)
            .Select(d => d.TypeDroit)
            .ToListAsync();
        return droits.ToHashSet();
    }

    public async Task<(bool, string?, Profil?)> CreerAsync(Profil profil)
    {
        var userId = await ActeurAsync(TypeDroit.ProfilGestion);
        if (userId == Ulid.Empty) return (false, MessageDroitGestion, null);

        profil.Code = (profil.Code ?? string.Empty).Trim().ToUpperInvariant();

        if (profil.Code.Length == 0) return (false, "Le code du profil est obligatoire.", null);
        if (string.IsNullOrWhiteSpace(profil.Libelle)) return (false, "Le libellé du profil est obligatoire.", null);

        await using var ctx = await _factory.CreateDbContextAsync();

        if (await ctx.Profils.AnyAsync(p => p.Code == profil.Code))
            return (false, $"Le code « {profil.Code} » est déjà utilisé par un autre profil.", null);

        profil.CreePar = userId;
        profil.DateCreation = DateTime.UtcNow;
        ctx.Profils.Add(profil);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(userId, null, "CREATION", "Securite", nameof(Profil),
            profil.Id.ToString(), null, new { profil.Code, profil.Libelle });

        return (true, null, profil);
    }

    public async Task<(bool, string?)> ModifierAsync(Profil profil)
    {
        var userId = await ActeurAsync(TypeDroit.ProfilGestion);
        if (userId == Ulid.Empty) return (false, MessageDroitGestion);

        await using var ctx = await _factory.CreateDbContextAsync();

        var existant = await ctx.Profils.FirstOrDefaultAsync(p => p.Id == profil.Id);
        if (existant is null) return (false, "Profil introuvable.");

        var avant = new { existant.Code, existant.Libelle, existant.EstActif };

        existant.Libelle     = profil.Libelle;
        existant.Description = profil.Description;
        existant.EstActif    = profil.EstActif;
        existant.ModifiePar  = userId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(userId, null, "MODIFICATION", "Securite", nameof(Profil),
            existant.Id.ToString(), avant, new { existant.Code, existant.Libelle, existant.EstActif });

        return (true, null);
    }

    public async Task<(bool, string?)> SupprimerAsync(Ulid profilId)
    {
        var userId = await ActeurAsync(TypeDroit.ProfilGestion);
        if (userId == Ulid.Empty) return (false, MessageDroitGestion);

        await using var ctx = await _factory.CreateDbContextAsync();

        var profil = await ctx.Profils.FirstOrDefaultAsync(p => p.Id == profilId);
        if (profil is null) return (false, "Profil introuvable.");
        if (profil.EstSysteme) return (false, "Ce profil est fourni par le socle et ne peut pas être supprimé.");

        var nbUtilisateurs = await ctx.UtilisateurProfils.CountAsync(up => up.ProfilId == profilId);
        if (nbUtilisateurs > 0)
            return (false, $"Ce profil est attribué à {nbUtilisateurs} utilisateur(s). Retirez-le d'abord.");

        // Suppression logique : le filtre global du DbContext le masquera partout.
        profil.EstSupprime = true;
        profil.DateSuppression = DateTime.UtcNow;
        profil.SupprimePar = userId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(userId, null, "SUPPRESSION", "Securite", nameof(Profil),
            profilId.ToString(), new { profil.Code, profil.Libelle }, null);

        return (true, null);
    }

    public async Task<(bool, string?)> DefinirDroitsAsync(Ulid profilId, IEnumerable<TypeDroit> droits)
    {
        var userId = await ActeurAsync(TypeDroit.ProfilGestion);
        if (userId == Ulid.Empty) return (false, MessageDroitGestion);

        var cibles = droits.Distinct().ToList();

        await using var ctx = await _factory.CreateDbContextAsync();

        var profil = await ctx.Profils.Include(p => p.Droits).FirstOrDefaultAsync(p => p.Id == profilId);
        if (profil is null) return (false, "Profil introuvable.");

        var actuels = profil.Droits.Select(d => d.TypeDroit).ToHashSet();

        var aRetirer = profil.Droits.Where(d => !cibles.Contains(d.TypeDroit)).ToList();
        ctx.ProfilDroits.RemoveRange(aRetirer);

        foreach (var droit in cibles.Where(d => !actuels.Contains(d)))
            ctx.ProfilDroits.Add(new ProfilDroit { ProfilId = profilId, TypeDroit = droit, CreePar = userId });

        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(userId, null, "MODIFICATION_DROITS", "Securite", nameof(Profil),
            profilId.ToString(), actuels.Select(d => d.ToString()), cibles.Select(d => d.ToString()));

        // Les droits en cache des porteurs de ce profil ne sont plus à jour.
        var porteurs = await ctx.UtilisateurProfils
            .Where(up => up.ProfilId == profilId)
            .Select(up => up.UtilisateurId)
            .ToListAsync();
        foreach (var id in porteurs) _droits.InvaliderCache(id);

        return (true, null);
    }
}

