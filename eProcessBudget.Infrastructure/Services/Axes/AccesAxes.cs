using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>
/// Contrôles communs aux services des axes de dépenses (cahier technique § 4.5) :
///   - droits : un droit de lecture et un droit de saisie par axe (EntretienLire,
///     EntretienSaisir...) ; la saisie donne la lecture ;
///   - périmètre : une saisie exige en plus une habilitation de saisie active couvrant
///     l'axe, la structure ou le site touchés (§ 2.6) ;
///   - les pièces budgétaires (dotations, engagements, paiements) restent celles du cycle
///     commun et de ses droits : les axes n'y ajoutent que leurs objets (projets, contrats,
///     avancement physique, inventaire) et leurs vues.
/// </summary>
public sealed class AccesAxes
{
    public const string ModuleAudit = "Axes";

    private readonly AccesParametrage _acces;
    private readonly IControlePerimetre _perimetre;

    public AccesAxes(AccesParametrage acces, IControlePerimetre perimetre)
    {
        _acces = acces;
        _perimetre = perimetre;
    }

    /// <summary>Utilisateur connecté s'il consulte l'axe, sinon Ulid.Empty.</summary>
    public Task<Ulid> LecteurAsync(AxeDepense axe) => _acces.ActeurAsync(axe.Lecture);

    /// <summary>Utilisateur connecté s'il consulte au moins un axe, sinon Ulid.Empty.</summary>
    public Task<Ulid> LecteurUnAxeAsync() => _acces.ActeurAsync(AxesDepense.LectureUnAxe);

    /// <summary>Utilisateur connecté s'il saisit sur l'axe, sinon Ulid.Empty.</summary>
    public Task<Ulid> SaisisseurAsync(AxeDepense axe) => _acces.ActeurAsync(axe.DroitSaisir);

    public Task<Ulid> ActeurAsync(params TypeDroit[] droits) => _acces.ActeurAsync(droits);

    public static string MessageLecture(AxeDepense axe) => $"Vous n'avez pas le droit de consulter l'axe {axe.Libelle}.";
    public static string MessageSaisie(AxeDepense axe) => $"Vous n'avez pas le droit de saisir sur l'axe {axe.Libelle}.";

    /// <summary>null si la saisie est dans le périmètre de l'utilisateur, sinon le message de refus.</summary>
    public Task<string?> PerimetreAsync(Ulid utilisateurId, CiblePerimetre cible) => _perimetre.VerifierSaisieAsync(utilisateurId, cible);

    /// <summary>Périmètre d'une saisie rattachée à un projet : son axe, sa structure et son site.</summary>
    internal async Task<string?> PerimetreProjetAsync(AppDbContext ctx, Ulid utilisateurId, Ulid projetId)
    {
        var p = await ctx.Projets.AsNoTracking().Where(x => x.Id == projetId)
            .Select(x => new { x.AxeId, x.StructureId, x.SiteId }).FirstOrDefaultAsync();
        return p is null
            ? "Projet introuvable."
            : await PerimetreAsync(utilisateurId, new CiblePerimetre(AxeId: p.AxeId, StructureId: p.StructureId, SiteId: p.SiteId));
    }

    /// <summary>Identifiant de l'axe dans l'organisme courant (null s'il manque au paramétrage).</summary>
    internal static Task<Ulid?> AxeIdAsync(AppDbContext ctx, AxeDepense axe)
        => ctx.Axes.AsNoTracking().Where(a => a.Code == axe.Code).Select(a => (Ulid?)a.Id).FirstOrDefaultAsync();

    /// <summary>L'axe (de dépenses) d'un identifiant de ref.Axe, ou null.</summary>
    internal static async Task<AxeDepense?> AxeDeIdAsync(AppDbContext ctx, Ulid axeId)
        => AxesDepense.DeCode(await ctx.Axes.AsNoTracking().Where(a => a.Id == axeId).Select(a => a.Code).FirstOrDefaultAsync());

    internal static string MessageAxeAbsent(AxeDepense axe)
        => $"L'axe {axe.Libelle} (code {axe.Code}) est absent du paramétrage de l'organisme.";
}
