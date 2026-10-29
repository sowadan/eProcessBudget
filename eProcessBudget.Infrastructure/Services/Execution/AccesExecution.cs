using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>
/// Contrôles communs aux services du cycle budgétaire (cahier technique § 4.4) :
///   - droits : ExecutionLire pour consulter ; DotationSaisir, AjustementSaisir / Valider,
///     EngagementSaisir / Valider, DepassementValider (Responsable Budget), PaiementSaisir
///     (Comptable), ExerciceCloturer ;
///   - périmètre : une saisie exige une habilitation de saisie active couvrant l'axe, la
///     structure ou le site de la dotation (§ 2.6) ;
///   - exercice : toute saisie exige un exercice ouvert (§ 4.2), sauf la prévision
///     initiale, qui se prépare avant l'ouverture.
/// </summary>
public sealed class AccesExecution
{
    public const string ModuleAudit = "Execution";

    /// <summary>Quiconque consulte ou intervient dans l'exécution budgétaire.</summary>
    public static readonly TypeDroit[] Lecture =
    [
        TypeDroit.ExecutionLire, TypeDroit.DotationSaisir, TypeDroit.AjustementSaisir, TypeDroit.AjustementValider,
        TypeDroit.EngagementSaisir, TypeDroit.EngagementValider, TypeDroit.DepassementValider, TypeDroit.PaiementSaisir,
        TypeDroit.ExerciceCloturer
    ];

    /// <summary>Destinataires des alertes et des demandes de décision : le Responsable Budget.</summary>
    public static readonly TypeDroit[] ResponsablesBudget = [TypeDroit.DepassementValider, TypeDroit.EngagementValider];

    private readonly AccesParametrage _acces;
    private readonly IControlePerimetre _perimetre;

    public AccesExecution(AccesParametrage acces, IControlePerimetre perimetre)
    {
        _acces = acces;
        _perimetre = perimetre;
    }

    public Task<Ulid> LecteurAsync() => _acces.ActeurAsync(Lecture);
    public Task<Ulid> ActeurAsync(params TypeDroit[] droits) => _acces.ActeurAsync(droits);

    public static string MessageDroit(string action) => $"Vous n'avez pas le droit de {action}.";

    /// <summary>null si la saisie sur cette dotation est dans le périmètre de l'utilisateur, sinon le message de refus.</summary>
    public async Task<string?> PerimetreAsync(AppDbContext ctx, Ulid utilisateurId, Ulid dotationId)
    {
        var d = await ctx.Dotations.AsNoTracking().Where(x => x.Id == dotationId)
            .Select(x => new { x.LigneBudgetaire.SousRubrique.Rubrique.AxeId, x.StructureId, x.SiteId })
            .FirstOrDefaultAsync();
        return d is null
            ? "Dotation introuvable."
            : await _perimetre.VerifierSaisieAsync(utilisateurId, new CiblePerimetre(AxeId: d.AxeId, StructureId: d.StructureId, SiteId: d.SiteId));
    }

    public Task<string?> PerimetreAsync(Ulid utilisateurId, CiblePerimetre cible) => _perimetre.VerifierSaisieAsync(utilisateurId, cible);

    /// <summary>L'exercice existe et est ouvert (règle § 4.2).</summary>
    internal static async Task<(Exercice? Exercice, string? Erreur)> ExerciceOuvertAsync(AppDbContext ctx, Ulid exerciceId)
    {
        var exercice = await ctx.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Id == exerciceId);
        if (exercice is null) return (null, "Exercice introuvable.");
        return exercice.Statut == StatutExercice.Ouvert ? (exercice, null) : (exercice, "La saisie exige un exercice ouvert.");
    }

    /// <summary>La date tombe dans l'exercice, et son mois n'est pas clos à la saisie.</summary>
    internal static async Task<string?> DateSaisissableAsync(AppDbContext ctx, Exercice exercice, DateOnly date, string libelle)
    {
        if (date < exercice.DateDebut || date > exercice.DateFin)
            return $"La date {libelle} doit être comprise dans l'exercice {exercice.Annee} ({exercice.DateDebut:dd/MM/yyyy} – {exercice.DateFin:dd/MM/yyyy}).";
        var moisClos = await ctx.Periodes.AnyAsync(p => p.ExerciceId == exercice.Id && p.TypePeriode == TypePeriode.Mois
                                                     && p.DateDebut <= date && p.DateFin >= date && p.EstCloturee);
        return moisClos ? $"Le mois du {date:dd/MM/yyyy} est clos à la saisie." : null;
    }

    /// <summary>
    /// Prochain numéro d'une série (préfixe, année) dans l'organisme courant. Les pièces
    /// supprimées comptent : un numéro n'est jamais réattribué.
    /// </summary>
    internal static async Task<string> NumeroSuivantAsync(IQueryable<string> numerosOrganisme, string prefixe, int annee)
    {
        var debut = $"{prefixe}-{annee}-";
        var dernier = await numerosOrganisme.Where(n => n.StartsWith(debut)).OrderByDescending(n => n).FirstOrDefaultAsync();
        return CalculsExecution.Numero(prefixe, annee, CalculsExecution.RangSuivant(dernier));
    }

    internal static bool EstConflitUnicite(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
}
