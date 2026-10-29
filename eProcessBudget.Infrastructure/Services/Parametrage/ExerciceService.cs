using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

public sealed record LigneExercice(
    Ulid Id, int Annee, DateOnly DateDebut, DateOnly DateFin, StatutExercice Statut,
    string? Cycle, DateTime? DateOuverture, DateTime? DateCloture, int NbMoisClotures);

public sealed record LignePeriode(Ulid Id, TypePeriode Type, int Numero, DateOnly DateDebut, DateOnly DateFin, bool EstCloturee);

/// <summary>
/// Exercices budgétaires et leurs périodes (cahier fonctionnel § 3.2.2, technique § 4.2).
///
/// Cycle de vie : En préparation → Ouvert → Clôturé. La création génère les 19 périodes
/// (12 mois, 4 trimestres, 2 semestres, l'année). La clôture (reliquats, encours, reports)
/// est un traitement du module Exécution, déclenché par le droit ExerciceCloturer.
/// L'ouverture fige la prévision initiale des dotations (cycle budgétaire, § 3.4).
/// </summary>
public interface IExerciceService
{
    Task<List<LigneExercice>> ListerAsync();
    Task<List<LignePeriode>> ListerPeriodesAsync(Ulid exerciceId);
    Task<(bool Ok, string? Erreur)> CreerAsync(int annee);
    Task<(bool Ok, string? Erreur)> OuvrirAsync(Ulid exerciceId);

    /// <summary>Ferme ou rouvre une période à la saisie (exercice ouvert uniquement).</summary>
    Task<(bool Ok, string? Erreur)> ChangerCloturePeriodeAsync(Ulid periodeId, bool cloturee);

    /// <summary>Supprime un exercice en préparation, s'il n'est pas utilisé.</summary>
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid exerciceId);
}

public sealed class ExerciceService : IExerciceService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IAuditService _audit;

    public ExerciceService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<LigneExercice>> ListerAsync()
    {
        if (await _acces.MembreOrganismeAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Exercices.AsNoTracking()
            .OrderByDescending(e => e.Annee)
            .Select(e => new LigneExercice(
                e.Id, e.Annee, e.DateDebut, e.DateFin, e.Statut,
                e.CycleProgrammation == null ? null : e.CycleProgrammation.Libelle,
                e.DateOuverture, e.DateCloture,
                e.Periodes.Count(p => p.TypePeriode == TypePeriode.Mois && p.EstCloturee)))
            .ToListAsync();
    }

    public async Task<List<LignePeriode>> ListerPeriodesAsync(Ulid exerciceId)
    {
        if (await _acces.MembreOrganismeAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Periodes.AsNoTracking()
            .Where(p => p.ExerciceId == exerciceId)
            .OrderBy(p => p.TypePeriode).ThenBy(p => p.Numero)
            .Select(p => new LignePeriode(p.Id, p.TypePeriode, p.Numero, p.DateDebut, p.DateFin, p.EstCloturee))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> CreerAsync(int annee)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);
        if (annee is < 2000 or > 2100) return (false, "L'année doit être comprise entre 2000 et 2100.");

        await using var ctx = await _factory.CreateDbContextAsync();
        if (await ctx.Exercices.AnyAsync(e => e.Annee == annee))
            return (false, $"L'exercice {annee} existe déjà.");

        var exercice = new Exercice
        {
            Annee = annee,
            DateDebut = new DateOnly(annee, 1, 1),
            DateFin = new DateOnly(annee, 12, 31),
            Statut = StatutExercice.EnPreparation,
            // Le cycle dont cet exercice est l'année de base, s'il existe déjà.
            CycleProgrammationId = await ctx.CyclesProgrammation
                .Where(c => c.AnneeBase == annee).Select(c => (Ulid?)c.Id).FirstOrDefaultAsync(),
            CreePar = acteurId
        };
        foreach (var periode in GenererPeriodes(annee))
        {
            periode.CreePar = acteurId;
            exercice.Periodes.Add(periode);
        }

        // Un seul SaveChanges : l'exercice et ses 19 périodes, ou rien.
        ctx.Exercices.Add(exercice);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CREATION", AccesParametrage.ModuleAudit, nameof(Exercice),
            exercice.Id.ToString(), null, new { exercice.Annee, Periodes = exercice.Periodes.Count });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> OuvrirAsync(Ulid exerciceId)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.FirstOrDefaultAsync(e => e.Id == exerciceId);
        if (exercice is null) return (false, "Exercice introuvable.");
        if (exercice.Statut != StatutExercice.EnPreparation) return (false, "Seul un exercice en préparation peut être ouvert.");

        exercice.Statut = StatutExercice.Ouvert;
        exercice.DateOuverture = DateTime.UtcNow;
        exercice.ModifiePar = acteurId;

        // La prévision initiale est figée à l'ouverture (§ 3.4) : elle n'évolue plus
        // ensuite que par des ajustements motivés.
        var dotations = await ctx.Dotations.Where(d => d.ExerciceId == exerciceId && d.Statut == StatutSaisie.Brouillon).ToListAsync();
        foreach (var d in dotations)
        {
            d.Statut = StatutSaisie.Valide;
            d.ModifiePar = acteurId;
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "OUVERTURE", AccesParametrage.ModuleAudit, nameof(Exercice),
            exercice.Id.ToString(), null, new { exercice.Annee, DotationsFigees = dotations.Count });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ChangerCloturePeriodeAsync(Ulid periodeId, bool cloturee)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var periode = await ctx.Periodes.Include(p => p.Exercice).FirstOrDefaultAsync(p => p.Id == periodeId);
        if (periode is null) return (false, "Période introuvable.");
        if (periode.Exercice.Statut != StatutExercice.Ouvert)
            return (false, "Les périodes ne se ferment et ne se rouvrent que sur un exercice ouvert.");
        if (periode.EstCloturee == cloturee) return (true, null);

        periode.EstCloturee = cloturee;
        periode.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, cloturee ? "CLOTURE_PERIODE" : "REOUVERTURE_PERIODE", AccesParametrage.ModuleAudit,
            nameof(Periode), periode.Id.ToString(), null,
            new { periode.Exercice.Annee, Type = periode.TypePeriode.ToString(), periode.Numero });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid exerciceId)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.Include(e => e.Periodes).FirstOrDefaultAsync(e => e.Id == exerciceId);
        if (exercice is null) return (false, "Exercice introuvable.");
        if (exercice.Statut != StatutExercice.EnPreparation) return (false, "Seul un exercice en préparation peut être supprimé.");

        // Les périodes appartiennent à l'exercice : elles ne bloquent pas, mais rien ne doit les utiliser.
        var utilisation = await VerificateurUtilisation.TrouverAsync(ctx, typeof(Exercice), exercice.Id, false, [typeof(Periode)]);
        foreach (var periode in exercice.Periodes)
            utilisation ??= await VerificateurUtilisation.TrouverAsync(ctx, typeof(Periode), periode.Id, false);
        if (utilisation is not null) return (false, $"Suppression impossible : l'exercice est utilisé par {utilisation}.");

        var maintenant = DateTime.UtcNow;
        foreach (var element in exercice.Periodes.Cast<BaseEntity>().Append(exercice))
        {
            element.EstSupprime = true;
            element.DateSuppression = maintenant;
            element.SupprimePar = acteurId;
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION", AccesParametrage.ModuleAudit, nameof(Exercice),
            exercice.Id.ToString(), new { exercice.Annee }, null);
        return (true, null);
    }

    /// <summary>Les 19 périodes d'une année civile : 12 mois, 4 trimestres, 2 semestres, l'année.</summary>
    internal static List<Periode> GenererPeriodes(int annee)
    {
        var periodes = new List<Periode>(19);

        void Ajouter(TypePeriode type, int numero, int moisDebut, int nbMois)
        {
            var debut = new DateOnly(annee, moisDebut, 1);
            periodes.Add(new Periode
            {
                TypePeriode = type,
                Numero = numero,
                DateDebut = debut,
                DateFin = debut.AddMonths(nbMois).AddDays(-1)
            });
        }

        for (var m = 1; m <= 12; m++) Ajouter(TypePeriode.Mois, m, m, 1);
        for (var t = 1; t <= 4; t++) Ajouter(TypePeriode.Trimestre, t, (t - 1) * 3 + 1, 3);
        for (var s = 1; s <= 2; s++) Ajouter(TypePeriode.Semestre, s, (s - 1) * 6 + 1, 6);
        Ajouter(TypePeriode.Annee, 1, 1, 12);

        return periodes;
    }
}
