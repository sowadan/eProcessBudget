using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>
/// Utilisateurs actifs d'un organisme qui détiennent l'un des droits : destinataires des
/// notifications « action requise » (validateurs d'habilitations, Responsable Budget...).
/// </summary>
public static class DetenteursDroit
{
    public static async Task<List<Ulid>> ListerAsync(AppDbContext ctx, IDroitService droits, Ulid organismeId, params TypeDroit[] typesDroit)
    {
        // Présélection en base (profils ou droits directs), puis droits effectifs (refus directs compris).
        var candidats = await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.OrganismeId == organismeId && u.EstActif && !u.EstAdministrateurPlateforme
                     && (u.UtilisateurProfils.Any(up => up.Profil.EstActif && up.Profil.Droits.Any(d => typesDroit.Contains(d.TypeDroit)))
                         || u.UtilisateurDroits.Any(d => d.EstAccorde && typesDroit.Contains(d.TypeDroit))))
            .Select(u => u.Id)
            .ToListAsync();

        var detenteurs = new List<Ulid>();
        foreach (var id in candidats)
            if (await droits.ADroitAsync(id, typesDroit)) detenteurs.Add(id);
        return detenteurs;
    }
}
