using System.Linq.Expressions;
using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>Anomalie d'un fichier importé, rattachée à sa ligne Excel.</summary>
public sealed record ErreurImport(int Ligne, string? Colonne, string Message);

/// <summary>Rapport d'un import : rien n'est enregistré dès qu'une ligne est en erreur.</summary>
public sealed record RapportImport(int NbLignes, int NbCrees, int NbModifies, IReadOnlyList<ErreurImport> Erreurs)
{
    public bool Ok => Erreurs.Count == 0;

    public static RapportImport Refus(string message) => new(0, 0, 0, [new ErreurImport(0, null, message)]);
}

/// <summary>
/// Contexte d'un import : résout les références saisies par code (ex. l'axe d'une
/// rubrique). Chaque référentiel cité est lu une seule fois, en deux colonnes (code, Id),
/// quel que soit le nombre de lignes du fichier.
/// </summary>
public sealed class ContexteImport
{
    private readonly AppDbContext _ctx;
    private readonly Dictionary<string, Dictionary<string, Ulid>> _caches = new();

    internal ContexteImport(AppDbContext ctx) => _ctx = ctx;

    public async Task<Ulid?> ResoudreAsync<TRef>(Expression<Func<TRef, string>> code, string valeur,
                                                 Expression<Func<TRef, bool>>? filtre = null, string? cleFiltre = null)
        where TRef : EntiteOrganisme
    {
        var cle = $"{typeof(TRef).Name}|{cleFiltre}";
        if (!_caches.TryGetValue(cle, out var table))
        {
            var q = _ctx.Set<TRef>().AsNoTracking();
            if (filtre is not null) q = q.Where(filtre);

            // Projection (code, Id) : le reste des colonnes n'est pas chargé.
            var x = code.Parameters[0];
            var projection = Expression.Lambda<Func<TRef, KeyValuePair<string, Ulid>>>(
                Expression.New(typeof(KeyValuePair<string, Ulid>).GetConstructor([typeof(string), typeof(Ulid)])!,
                               code.Body, Expression.Property(x, nameof(BaseEntity.Id))), x);

            var paires = await q.Select(projection).ToListAsync();
            table = new Dictionary<string, Ulid>(StringComparer.OrdinalIgnoreCase);
            foreach (var (c, id) in paires) table.TryAdd(c, id);
            _caches[cle] = table;
        }

        return table.TryGetValue(valeur, out var trouve) ? trouve : null;
    }

    private Dictionary<string, Ulid>? _utilisateurs;

    /// <summary>Compte actif de l'organisme courant, par identifiant de connexion.</summary>
    public async Task<Ulid?> ResoudreUtilisateurAsync(string matricule)
    {
        if (_utilisateurs is null)
        {
            var organismeId = _ctx.OrganismeCourantId;
            _utilisateurs = (await _ctx.Utilisateurs.AsNoTracking()
                    .Where(u => u.OrganismeId == organismeId && u.EstActif)
                    .Select(u => new { u.Matricule, u.Id })
                    .ToListAsync())
                .ToDictionary(u => u.Matricule, u => u.Id, StringComparer.OrdinalIgnoreCase);
        }
        return _utilisateurs.TryGetValue(matricule, out var id) ? id : null;
    }
}
