using eProcessBudget.Infrastructure.Services.Execution;

namespace eProcessBudget.Tests.Outils;

/// <summary>
/// Réponse du métier C.2.2 : tout décaissement se rapporte à une facture reçue. Les tests qui
/// paient passent par <see cref="PayerAsync"/> : le paiement porte sur une facture non soldée
/// de l'engagement, ou à défaut sur une facture du montant enregistrée pour l'occasion.
/// </summary>
public static class Factures
{
    /// <summary>Paie sur une facture ; renvoie la première erreur rencontrée si aucun essai n'aboutit.</summary>
    public static async Task<(bool Ok, string? Erreur)> PayerAsync(this IPaiementService service, SaisiePaiement s)
    {
        string? premiereErreur = null;
        foreach (var facture in await service.ListerFacturesAsync(s.EngagementId))
        {
            s.FactureId = facture.Id;
            var essai = await service.EnregistrerAsync(s);
            if (essai.Ok) return essai;
            premiereErreur ??= essai.Erreur;
        }

        var numero = $"F-{s.NumeroPiece}-{Ulid.NewUlid().ToString()[^6..]}";
        var ajout = await service.AjouterFactureAsync(new SaisieFacture
        {
            EngagementId = s.EngagementId, Numero = numero, DateFacture = s.Date, DateReception = s.Date, Montant = s.Montant
        });
        s.FactureId = ajout.Ok ? (await service.ListerFacturesAsync(s.EngagementId)).First(f => f.Libelle.StartsWith(numero)).Id : null;
        var resultat = await service.EnregistrerAsync(s);
        return resultat.Ok ? resultat : (false, premiereErreur ?? resultat.Erreur);
    }
}
