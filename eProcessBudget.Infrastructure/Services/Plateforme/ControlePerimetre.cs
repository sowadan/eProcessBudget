using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Plateforme;

/// <summary>
/// Éléments que touche une saisie. Elle est admise si une habilitation de saisie active
/// couvre l'organisme entier, ou l'un de ces éléments.
/// </summary>
public sealed record CiblePerimetre(
    Ulid? ProgrammeId = null, Ulid? ActionId = null, Ulid? AxeId = null, Ulid? StructureId = null, Ulid? SiteId = null)
{
    public static readonly CiblePerimetre Organisme = new();
}

/// <summary>
/// Filtre de périmètre des saisies (cahier technique § 2.6) : en plus du droit, une
/// saisie exige une habilitation de saisie ACTIVE (double validation, période en cours)
/// sur l'organisme entier ou sur l'un des éléments touchés.
///
/// Point unique : chaque service de saisie l'interroge avant d'écrire. Les définitions
/// (programmes, indicateurs, référentiels) n'y sont pas soumises : seuls les droits les
/// gouvernent.
/// </summary>
public interface IControlePerimetre
{
    /// <summary>null si la saisie est admise, sinon le message à afficher.</summary>
    Task<string?> VerifierSaisieAsync(Ulid utilisateurId, CiblePerimetre cible);

    /// <summary>
    /// Périmètre de lecture des vues consolidées (consommation, tableau de bord) : l'union
    /// des habilitations actives, de lecture ou de saisie.
    /// </summary>
    Task<PerimetreLecture> PerimetreLectureAsync(Ulid utilisateurId);
}

/// <summary>
/// Éléments que l'utilisateur peut consulter dans les vues consolidées. <see cref="Complet"/> :
/// tout l'organisme (habilitation sur l'organisme entier, ou aucune habilitation : la lecture
/// repose alors sur les seuls droits du profil).
/// </summary>
public sealed record PerimetreLecture(
    bool Complet, IReadOnlySet<Ulid> Axes, IReadOnlySet<Ulid> Programmes, IReadOnlySet<Ulid> Actions,
    IReadOnlySet<Ulid> Sites, IReadOnlySet<Ulid> Structures)
{
    public static readonly PerimetreLecture Tout = new(true, new HashSet<Ulid>(), new HashSet<Ulid>(), new HashSet<Ulid>(), new HashSet<Ulid>(), new HashSet<Ulid>());

    /// <summary>Clé courte du périmètre, pour un cache.</summary>
    public string Cle => Complet ? "*" : string.Join(",", Axes.Concat(Programmes).Concat(Actions).Concat(Sites).Concat(Structures).Order());
}

public sealed class ControlePerimetre : IControlePerimetre
{
    public const string MessageRefus =
        "Aucune habilitation de saisie active ne couvre cet élément. " +
        "Demandez-la dans Administration › Habilitations (double validation).";

    private readonly IDbContextFactory<AppDbContext> _factory;

    public ControlePerimetre(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    public async Task<string?> VerifierSaisieAsync(Ulid utilisateurId, CiblePerimetre cible)
    {
        if (utilisateurId == Ulid.Empty) return MessageRefus;

        var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var ctx = await _factory.CreateDbContextAsync();

        // Habilitations de saisie actives de l'utilisateur, dans l'organisme courant (filtre global).
        var perimetres = await ctx.Habilitations.AsNoTracking()
            .Where(h => h.UtilisateurId == utilisateurId
                     && h.Statut == StatutHabilitation.Active
                     && h.NiveauAcces == NiveauAcces.Saisie
                     && h.DateDebut <= aujourdhui
                     && (h.DateFin == null || h.DateFin >= aujourdhui))
            .Select(h => new { h.TypePerimetre, h.PerimetreId })
            .ToListAsync();

        var admis = perimetres.Any(p => p.TypePerimetre switch
        {
            TypePerimetre.OrganismeEntier => true,
            TypePerimetre.Programme => p.PerimetreId == cible.ProgrammeId && cible.ProgrammeId is not null,
            TypePerimetre.Action => p.PerimetreId == cible.ActionId && cible.ActionId is not null,
            TypePerimetre.Axe => p.PerimetreId == cible.AxeId && cible.AxeId is not null,
            TypePerimetre.Structure => p.PerimetreId == cible.StructureId && cible.StructureId is not null,
            TypePerimetre.Site => p.PerimetreId == cible.SiteId && cible.SiteId is not null,
            _ => false
        });

        return admis ? null : MessageRefus;
    }

    public async Task<PerimetreLecture> PerimetreLectureAsync(Ulid utilisateurId)
    {
        var aujourdhui = DateOnly.FromDateTime(DateTime.UtcNow);
        await using var ctx = await _factory.CreateDbContextAsync();
        var perimetres = await ctx.Habilitations.AsNoTracking()
            .Where(h => h.UtilisateurId == utilisateurId
                     && h.Statut == StatutHabilitation.Active
                     && h.DateDebut <= aujourdhui
                     && (h.DateFin == null || h.DateFin >= aujourdhui))
            .Select(h => new { h.TypePerimetre, h.PerimetreId })
            .ToListAsync();

        // Sans habilitation, la lecture suit les droits du profil ; une habilitation la restreint.
        if (perimetres.Count == 0 || perimetres.Any(p => p.TypePerimetre == TypePerimetre.OrganismeEntier)) return PerimetreLecture.Tout;

        HashSet<Ulid> De(TypePerimetre type)
            => perimetres.Where(p => p.TypePerimetre == type && p.PerimetreId is not null).Select(p => p.PerimetreId!.Value).ToHashSet();
        return new PerimetreLecture(false, De(TypePerimetre.Axe), De(TypePerimetre.Programme), De(TypePerimetre.Action),
                                    De(TypePerimetre.Site), De(TypePerimetre.Structure));
    }
}
