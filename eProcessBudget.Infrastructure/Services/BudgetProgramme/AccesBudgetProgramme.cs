using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

/// <summary>
/// Contrôles communs aux services du budget-programme (cahier technique § 4.3) :
///   - droits : BudgetProgrammeLire (consulter), BudgetProgrammeDefinir (programmes,
///     indicateurs, cadre logique), ProgrammationSaisir (AE/CP, ressources, emplois, clés,
///     temps), IndicateurSaisirRealisation (réalisations des indicateurs) ;
///   - périmètre : une saisie exige en plus une habilitation de saisie active (§ 2.6) ;
///   - exercice et cycle : on ne saisit que dans un exercice ouvert, on ne programme que
///     dans un cycle en brouillon.
/// </summary>
public sealed class AccesBudgetProgramme
{
    public const string ModuleAudit = "BudgetProgramme";

    private readonly AccesParametrage _acces;
    private readonly IControlePerimetre _perimetre;

    public AccesBudgetProgramme(AccesParametrage acces, IControlePerimetre perimetre)
    {
        _acces = acces;
        _perimetre = perimetre;
    }

    public Task<Ulid> LecteurAsync() => _acces.ActeurAsync(CatalogueReferentiels.LectureBudgetProgramme);
    public Task<Ulid> ActeurAsync(params TypeDroit[] droits) => _acces.ActeurAsync(droits);

    /// <summary>null si la saisie est admise sur ce périmètre, sinon le message de refus.</summary>
    public Task<string?> PerimetreAsync(Ulid utilisateurId, CiblePerimetre cible) => _perimetre.VerifierSaisieAsync(utilisateurId, cible);

    public static string MessageDroit(string action) => $"Vous n'avez pas le droit de {action}.";

    /// <summary>Le cycle existe et accepte encore des modifications (brouillon).</summary>
    internal static async Task<(CycleProgrammation? Cycle, string? Erreur)> CycleModifiableAsync(AppDbContext ctx, Ulid cycleId)
    {
        var cycle = await ctx.CyclesProgrammation.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null) return (null, "Cycle de programmation introuvable.");
        return cycle.Statut == StatutCycle.Brouillon
            ? (cycle, null)
            : (cycle, "Ce cycle est validé ou archivé : sa programmation n'est plus modifiable. Reconduisez-le pour préparer le suivant.");
    }

    /// <summary>
    /// L'exercice existe et accepte la saisie (règle § 4.2) : ouvert, ou en préparation
    /// quand <paramref name="preparationAdmise"/> (clés de répartition, temps des agents,
    /// qui se préparent avant l'ouverture).
    /// </summary>
    internal static async Task<(Exercice? Exercice, string? Erreur)> ExerciceSaisissableAsync(AppDbContext ctx, Ulid exerciceId, bool preparationAdmise = false)
    {
        var exercice = await ctx.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Id == exerciceId);
        if (exercice is null) return (null, "Exercice introuvable.");
        var admis = exercice.Statut == StatutExercice.Ouvert
                 || (preparationAdmise && exercice.Statut == StatutExercice.EnPreparation);
        return admis
            ? (exercice, null)
            : (exercice, preparationAdmise ? "L'exercice est clôturé : plus aucune saisie." : "La saisie exige un exercice ouvert.");
    }

    /// <summary>Années d'un cycle : référence N-1, base N, N+1, N+2.</summary>
    internal static int[] Annees(CycleProgrammation c) => Enumerable.Range(c.AnneeReference, c.AnneeFin - c.AnneeReference + 1).ToArray();
}
