using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Recettes;

/// <summary>
/// Contrôles communs aux services des recettes (cahier technique § 4.6) :
///   - droits : RecettesLire pour consulter ; PrevisionRecetteSaisir pour les prévisions,
///     leurs révisions, l'échéancier et les reports de ressources ; EncaissementSaisir pour
///     la saisie mensuelle des encaissements. Chaque droit de saisie donne la lecture ;
///   - périmètre : une saisie exige une habilitation de saisie active couvrant l'axe de la
///     ligne de recette ou le site (§ 2.6).
/// </summary>
public sealed class AccesRecettes
{
    public const string ModuleAudit = "Recettes";

    /// <summary>Quiconque consulte ou saisit dans les recettes.</summary>
    public static readonly TypeDroit[] Lecture = [TypeDroit.RecettesLire, TypeDroit.PrevisionRecetteSaisir, TypeDroit.EncaissementSaisir];

    /// <summary>Destinataires des alertes de recouvrement : qui prévoit et qui encaisse.</summary>
    public static readonly TypeDroit[] Gestionnaires = [TypeDroit.PrevisionRecetteSaisir, TypeDroit.EncaissementSaisir];

    private readonly AccesParametrage _acces;
    private readonly IControlePerimetre _perimetre;

    public AccesRecettes(AccesParametrage acces, IControlePerimetre perimetre)
    {
        _acces = acces;
        _perimetre = perimetre;
    }

    public Task<Ulid> LecteurAsync() => _acces.ActeurAsync(Lecture);
    public Task<Ulid> ActeurAsync(params TypeDroit[] droits) => _acces.ActeurAsync(droits);

    public static string MessageDroit(string action) => $"Vous n'avez pas le droit de {action}.";

    /// <summary>null si la saisie est dans le périmètre de l'utilisateur, sinon le message de refus.</summary>
    public Task<string?> PerimetreAsync(Ulid utilisateurId, Ulid? axeId, Ulid? siteId)
        => _perimetre.VerifierSaisieAsync(utilisateurId, new CiblePerimetre(AxeId: axeId, SiteId: siteId));

    /// <summary>Périmètre d'une saisie sur une prévision : l'axe de sa ligne et son site.</summary>
    internal async Task<string?> PerimetrePrevisionAsync(AppDbContext ctx, Ulid utilisateurId, Ulid previsionId)
    {
        var p = await ctx.PrevisionsRecettes.AsNoTracking().Where(x => x.Id == previsionId)
            .Select(x => new { x.LigneBudgetaire.SousRubrique.Rubrique.AxeId, x.SiteId }).FirstOrDefaultAsync();
        return p is null ? "Prévision introuvable." : await PerimetreAsync(utilisateurId, p.AxeId, p.SiteId);
    }
}
