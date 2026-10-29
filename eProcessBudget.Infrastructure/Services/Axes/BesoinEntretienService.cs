using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

public sealed record LigneBesoin(Ulid Id, Ulid? TypeTravauxId, string TypeTravaux, decimal Montant, decimal? Km, string? Commentaire);

public sealed class SaisieBesoin
{
    public Ulid Id { get; set; }
    public Ulid ExerciceId { get; set; }

    /// <summary>Type de travaux ; vide pour un besoin global.</summary>
    public Ulid? TypeTravauxId { get; set; }

    public decimal Montant { get; set; }
    public decimal? Km { get; set; }
    public string? Commentaire { get; set; }
}

/// <summary>
/// Besoins d'entretien routier évalués par exercice (réponse du métier C.2.1), global ou par
/// type de travaux, et indicateurs de l'entretien : conformité (AD / prévisions affectées),
/// couverture des besoins (financement / besoins évalués), exécution physique (CP / AD).
/// Lecture : axe Entretien routier ; saisie : droit de saisie de l'axe, exercice non clôturé.
/// </summary>
public interface IBesoinEntretienService
{
    Task<List<LigneBesoin>> ListerAsync(Ulid exerciceId);
    Task<SaisieBesoin?> ObtenirSaisieAsync(Ulid besoinId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieBesoin saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid besoinId);

    /// <summary>Indicateurs de l'entretien de l'exercice ; null sans droit de lecture de l'axe.</summary>
    Task<IndicateursEntretien?> IndicateursAsync(Ulid exerciceId);
}

public sealed class BesoinEntretienService : IBesoinEntretienService
{
    private static readonly AxeDepense Axe = AxesDepense.Entretien;
    private const string LibelleGlobal = "Tous types de travaux";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesAxes _acces;
    private readonly IAuditService _audit;

    public BesoinEntretienService(IDbContextFactory<AppDbContext> factory, AccesAxes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<LigneBesoin>> ListerAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync(Axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.BesoinsEntretien.AsNoTracking().Where(b => b.ExerciceId == exerciceId)
            .OrderBy(b => b.TypeTravaux == null ? "" : b.TypeTravaux.Code)
            .Select(b => new LigneBesoin(b.Id, b.TypeTravauxId, b.TypeTravaux != null ? b.TypeTravaux.Libelle : LibelleGlobal, b.Montant, b.Km, b.Commentaire))
            .ToListAsync();
    }

    public async Task<SaisieBesoin?> ObtenirSaisieAsync(Ulid besoinId)
    {
        if (await _acces.LecteurAsync(Axe) == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.BesoinsEntretien.AsNoTracking().Where(b => b.Id == besoinId)
            .Select(b => new SaisieBesoin { Id = b.Id, ExerciceId = b.ExerciceId, TypeTravauxId = b.TypeTravauxId, Montant = b.Montant, Km = b.Km, Commentaire = b.Commentaire })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieBesoin s)
    {
        var acteurId = await _acces.SaisisseurAsync(Axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(Axe));
        if (s.Montant < 0 || !CalculsExecution.EstMontantValide(s.Montant)) return (false, "Le montant des besoins est positif, à 2 décimales au plus.");
        if (s.Km is { } km && (km < 0 || decimal.Round(km, 4) != km)) return (false, "Le kilométrage est positif, à 4 décimales au plus.");
        var commentaire = string.IsNullOrWhiteSpace(s.Commentaire) ? null : s.Commentaire.Trim();
        if (commentaire?.Length > 1000) return (false, "Commentaire : 1 000 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var statut = await ctx.Exercices.Where(e => e.Id == s.ExerciceId).Select(e => (StatutExercice?)e.Statut).FirstOrDefaultAsync();
        if (statut is null) return (false, "Exercice introuvable.");
        if (statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé : ses besoins ne se modifient plus.");
        if (s.TypeTravauxId is { } type && !await ctx.TypesTravaux.AnyAsync(t => t.Id == type && t.EstActif && !t.EstPrestationControle))
            return (false, "Type de travaux introuvable, inactif, ou qui est une prestation de contrôle.");
        if (await ctx.BesoinsEntretien.AnyAsync(b => b.ExerciceId == s.ExerciceId && b.TypeTravauxId == s.TypeTravauxId && b.Id != s.Id))
            return (false, s.TypeTravauxId is null ? "Le besoin global de l'exercice est déjà saisi." : "Le besoin de ce type de travaux est déjà saisi pour l'exercice.");
        // Besoins saisis soit globalement, soit par type de travaux : jamais les deux (pas de double compte).
        if (await ctx.BesoinsEntretien.AnyAsync(b => b.ExerciceId == s.ExerciceId && b.Id != s.Id && (b.TypeTravauxId == null) != (s.TypeTravauxId == null)))
            return (false, "Les besoins de l'exercice se saisissent soit globalement, soit par type de travaux, pas les deux.");

        BesoinEntretien besoin;
        if (s.Id == Ulid.Empty)
        {
            besoin = new BesoinEntretien { ExerciceId = s.ExerciceId, CreePar = acteurId };
            ctx.BesoinsEntretien.Add(besoin);
        }
        else
        {
            var existant = await ctx.BesoinsEntretien.FirstOrDefaultAsync(b => b.Id == s.Id);
            if (existant is null || existant.ExerciceId != s.ExerciceId) return (false, "Besoin introuvable.");
            besoin = existant;
        }
        besoin.TypeTravauxId = s.TypeTravauxId;
        besoin.Montant = s.Montant;
        besoin.Km = s.Km;
        besoin.Commentaire = commentaire;
        besoin.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid besoinId)
    {
        var acteurId = await _acces.SaisisseurAsync(Axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(Axe));
        await using var ctx = await _factory.CreateDbContextAsync();
        var besoin = await ctx.BesoinsEntretien.Include(b => b.Exercice).FirstOrDefaultAsync(b => b.Id == besoinId);
        if (besoin is null) return (false, "Besoin introuvable.");
        if (besoin.Exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé : ses besoins ne se modifient plus.");
        besoin.EstSupprime = true;
        besoin.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        return (true, null);
    }

    public async Task<IndicateursEntretien?> IndicateursAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync(Axe) == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await CalculerAsync(ctx, ctx.Dotations.AsNoTracking().Where(d => d.ExerciceId == exerciceId), exerciceId);
    }

    /// <summary>Dotations qui financent l'entretien : axe Entretien routier et projets de réhabilitation.</summary>
    internal static IQueryable<Dotation> Financement(IQueryable<Dotation> q)
        => q.Where(d => d.LigneBudgetaire.SousRubrique.Rubrique.Axe.Code == AxesDepense.Entretien.Code
                     || (d.Projet != null && d.Projet.Travaux != null && d.Projet.Axe.Code == AxesDepense.Investissement.Code));

    /// <summary>Indicateurs de l'entretien sur les dotations données (déjà restreintes à l'exercice et au périmètre).</summary>
    internal static async Task<IndicateursEntretien> CalculerAsync(AppDbContext ctx, IQueryable<Dotation> dotations, Ulid exerciceId, DateOnly? jusquAu = null)
    {
        var situations = await SituationService.Calculer(ctx, Financement(dotations), jusquAu).ToListAsync();
        var besoins = await ctx.BesoinsEntretien.AsNoTracking().Where(b => b.ExerciceId == exerciceId).SumAsync(b => (decimal?)b.Montant) ?? 0;
        return new IndicateursEntretien(MontantsDotation.Somme(situations.Select(d => d.Montants)), besoins);
    }
}
