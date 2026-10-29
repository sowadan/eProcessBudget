using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;

namespace eProcessBudget.Infrastructure.Services.Consommation;

/// <summary>
/// Contrôle d'accès des vues consolidées (cahier technique § 4.7) : ConsommationLire pour la
/// consommation budgétaire, TableauDeBordLire pour le tableau de bord, « restreints au
/// périmètre » : les données sont limitées aux habilitations actives de l'utilisateur (§ 2.6,
/// voir <see cref="IControlePerimetre.PerimetreLectureAsync"/>).
/// </summary>
public sealed class AccesConsommation
{
    private readonly AccesParametrage _acces;
    private readonly IControlePerimetre _perimetre;

    public AccesConsommation(AccesParametrage acces, IControlePerimetre perimetre)
    {
        _acces = acces;
        _perimetre = perimetre;
    }

    /// <summary>Utilisateur et périmètre de lecture s'il détient l'un des droits, sinon null.</summary>
    public async Task<(Ulid UtilisateurId, PerimetreLecture Perimetre)?> LecteurAsync(params TypeDroit[] droits)
    {
        var utilisateurId = await _acces.ActeurAsync(droits);
        if (utilisateurId == Ulid.Empty) return null;
        return (utilisateurId, await _perimetre.PerimetreLectureAsync(utilisateurId));
    }
}
