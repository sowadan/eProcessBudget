using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>Critères de la liste des avancements d'un exercice.</summary>
public sealed class FiltreAvancements
{
    public Ulid ExerciceId { get; set; }
    public Ulid? ProjetId { get; set; }
    public Ulid? TypeTravauxId { get; set; }
}

public sealed record LigneAvancement(
    Ulid Id, Ulid ProjetId, string Projet, Ulid PeriodeId, DateOnly Mois, Ulid? ContratId, string? Contrat,
    Ulid? TypeTravauxId, string? TypeTravaux, decimal? KmRealises, decimal? TauxAvancement, string? Commentaire);

public sealed class SaisieAvancement
{
    public Ulid Id { get; set; }
    public Ulid? ProjetId { get; set; }
    public Ulid? ContratId { get; set; }
    public Ulid? PeriodeId { get; set; }

    /// <summary>Par défaut, celui de l'opération.</summary>
    public Ulid? TypeTravauxId { get; set; }

    public decimal? KmRealises { get; set; }
    public decimal? TauxAvancement { get; set; }
    public string? Commentaire { get; set; }
}

/// <summary>Kilométrage d'un type de travaux : prévu par les opérations, réalisé sur l'exercice et en cumul.</summary>
public sealed record SyntheseKm(Ulid? TypeTravauxId, string TypeTravaux, decimal KmPrevus, decimal KmExercice, decimal KmCumules)
{
    /// <summary>Taux d'avancement kilométrique (§ 4.1) : km réalisés cumulés / km prévus.</summary>
    public decimal? Taux => CalculsAxes.TauxAvancementKm(KmCumules, KmPrevus);
}

/// <summary>
/// Avancement physique des opérations de travaux : axe Entretien routier, et projets de
/// réhabilitation de l'axe Investissements (réponse C.2.4) (cahier fonctionnel
/// § 3.5.2 et 3.5.4, technique § 4.5) : kilomètres entretenus et taux d'avancement, saisis
/// par opération, mois, contrat et type de travaux (C.2.11). Indicateur : kilométrage
/// réalisé / prévu.
/// </summary>
public interface IAvancementService
{
    Task<(List<LigneAvancement> Items, int Total)> ListerAsync(FiltreAvancements filtre, int page = 1, int taille = 20);

    /// <summary>Kilométrage prévu et réalisé par type de travaux.</summary>
    Task<List<SyntheseKm>> SyntheseAsync(Ulid exerciceId);

    Task<SaisieAvancement?> ObtenirSaisieAsync(Ulid avancementId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieAvancement saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid avancementId);
}

public sealed class AvancementService : IAvancementService
{
    /// <summary>Axes qui portent des opérations de travaux : entretien, et réhabilitation en investissement (C.2.4).</summary>
    private static readonly AxeDepense[] AxesTravaux = [AxesDepense.Entretien, AxesDepense.Investissement];

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesAxes _acces;
    private readonly IAuditService _audit;

    public AvancementService(IDbContextFactory<AppDbContext> factory, AccesAxes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    /// <summary>Codes des axes de travaux que l'utilisateur consulte.</summary>
    private async Task<List<string>> AxesLisiblesAsync()
    {
        var codes = new List<string>();
        foreach (var axe in AxesTravaux)
            if (await _acces.LecteurAsync(axe) != Ulid.Empty) codes.Add(axe.Code);
        return codes;
    }

    public async Task<(List<LigneAvancement> Items, int Total)> ListerAsync(FiltreAvancements f, int page = 1, int taille = 20)
    {
        var axes = await AxesLisiblesAsync();
        if (axes.Count == 0) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.AvancementsPhysiques.AsNoTracking().Where(a => a.Periode.ExerciceId == f.ExerciceId && axes.Contains(a.Projet.Axe.Code));
        if (f.ProjetId is { } projet) q = q.Where(a => a.ProjetId == projet);
        if (f.TypeTravauxId is { } tt) q = q.Where(a => a.TypeTravauxId == tt);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.Periode.DateDebut).ThenBy(a => a.Projet.Code)
            .Skip((page - 1) * taille).Take(taille)
            .Select(a => new LigneAvancement(a.Id, a.ProjetId, a.Projet.Code + " — " + a.Projet.Intitule, a.PeriodeId, a.Periode.DateDebut,
                a.ContratId, a.Contrat != null ? a.Contrat.Numero : null,
                a.TypeTravauxId, a.TypeTravaux != null ? a.TypeTravaux.Libelle : null,
                a.KmRealises, a.TauxAvancement, a.Commentaire))
            .ToListAsync();
        return (items, total);
    }

    public async Task<List<SyntheseKm>> SyntheseAsync(Ulid exerciceId)
    {
        var axes = await AxesLisiblesAsync();
        if (axes.Count == 0) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var prevus = await ctx.ProjetsTravaux.AsNoTracking()
            .Where(t => t.Projet.Statut != StatutProjet.Annule && axes.Contains(t.Projet.Axe.Code))
            .GroupBy(t => t.TypeTravauxId)
            .Select(g => new { g.Key, Km = g.Sum(t => t.KmPrevus ?? 0) })
            .ToListAsync();
        var cumules = await ctx.AvancementsPhysiques.AsNoTracking()
            .Where(a => axes.Contains(a.Projet.Axe.Code))
            .GroupBy(a => a.TypeTravauxId)
            .Select(g => new { g.Key, Km = g.Sum(a => a.KmRealises ?? 0) })
            .ToListAsync();
        var exercice = await ctx.AvancementsPhysiques.AsNoTracking()
            .Where(a => a.Periode.ExerciceId == exerciceId && axes.Contains(a.Projet.Axe.Code))
            .GroupBy(a => a.TypeTravauxId)
            .Select(g => new { g.Key, Km = g.Sum(a => a.KmRealises ?? 0) })
            .ToListAsync();

        var types = prevus.Select(p => p.Key).Union(cumules.Select(r => r.Key)).ToList();
        var libelles = await ctx.TypesTravaux.AsNoTracking()
            .Where(t => types.Contains(t.Id))
            .Select(t => new { t.Id, t.Libelle, t.Ordre })
            .ToDictionaryAsync(t => t.Id);

        return types
            .Select(t => new SyntheseKm(t,
                t is { } id && libelles.TryGetValue(id, out var l) ? l.Libelle : "Type de travaux non précisé",
                prevus.Where(p => p.Key == t).Sum(p => p.Km),
                exercice.Where(r => r.Key == t).Sum(r => r.Km),
                cumules.Where(r => r.Key == t).Sum(r => r.Km)))
            .OrderBy(s => s.TypeTravauxId is { } id && libelles.TryGetValue(id, out var l) ? l.Ordre : int.MaxValue)
            .ThenBy(s => s.TypeTravaux)
            .ToList();
    }

    public async Task<SaisieAvancement?> ObtenirSaisieAsync(Ulid avancementId)
    {
        var axes = await AxesLisiblesAsync();
        if (axes.Count == 0) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.AvancementsPhysiques.AsNoTracking().Where(a => a.Id == avancementId && axes.Contains(a.Projet.Axe.Code))
            .Select(a => new SaisieAvancement
            {
                Id = a.Id, ProjetId = a.ProjetId, ContratId = a.ContratId, PeriodeId = a.PeriodeId, TypeTravauxId = a.TypeTravauxId,
                KmRealises = a.KmRealises, TauxAvancement = a.TauxAvancement, Commentaire = a.Commentaire
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieAvancement s)
    {
        if (s.ProjetId is not { } projetId) return (false, "Choisissez l'opération de travaux.");
        if (s.PeriodeId is not { } periodeId) return (false, "Choisissez le mois.");
        if (s.KmRealises is null && s.TauxAvancement is null) return (false, "Indiquez les kilomètres entretenus ou le taux d'avancement.");
        if (s.KmRealises is { } km && (km < 0 || decimal.Round(km, 4) != km))
            return (false, "Les kilomètres entretenus sont un nombre positif, à 4 décimales au plus.");
        if (s.TauxAvancement is { } taux && (taux < 0 || taux > 100 || decimal.Round(taux, 4) != taux))
            return (false, "Le taux d'avancement est compris entre 0 et 100 %.");
        var commentaire = string.IsNullOrWhiteSpace(s.Commentaire) ? null : s.Commentaire.Trim();
        if (commentaire?.Length > 1000) return (false, "Commentaire : 1 000 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var projet = await ctx.Projets.AsNoTracking().Where(p => p.Id == projetId)
            .Select(p => new { p.AxeId, CodeAxe = p.Axe.Code, p.StructureId, p.SiteId, p.Statut, EstTravaux = p.Travaux != null, TypeTravauxId = p.Travaux != null ? p.Travaux.TypeTravauxId : null })
            .FirstOrDefaultAsync();
        if (projet is null) return (false, "Opération introuvable.");
        if (AxesTravaux.FirstOrDefault(a => a.Code == projet.CodeAxe) is not { } axe || !projet.EstTravaux)
            return (false, "L'avancement physique se saisit sur une opération de travaux : entretien routier, ou réhabilitation en investissement.");
        var acteurId = await _acces.SaisisseurAsync(axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(axe));
        if (projet.Statut == StatutProjet.Annule) return (false, "L'opération est annulée.");

        var periode = await ctx.Periodes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodeId);
        if (periode is null || periode.TypePeriode != TypePeriode.Mois) return (false, "L'avancement se saisit par mois.");
        var (exercice, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, periode.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var erreurDate = await AccesExecution.DateSaisissableAsync(ctx, exercice!, periode.DateDebut, "du mois");
        if (erreurDate is not null) return (false, erreurDate);

        if (s.ContratId is { } contratId
            && !await ctx.Contrats.AnyAsync(c => c.Id == contratId && c.ProjetId == projetId && c.TypeContrat == TypeContrat.Travaux))
            return (false, "Le contrat doit être un contrat de travaux de cette opération.");
        var typeTravauxId = s.TypeTravauxId ?? projet.TypeTravauxId;
        if (typeTravauxId is { } tt && !await ctx.TypesTravaux.AnyAsync(t => t.Id == tt && t.EstActif && !t.EstPrestationControle))
            return (false, "Type de travaux introuvable, inactif, ou qui est une prestation de contrôle.");

        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: projet.AxeId, StructureId: projet.StructureId, SiteId: projet.SiteId));
        if (refus is not null) return (false, refus);

        // Un relevé par opération, mois, contrat et type de travaux.
        if (await ctx.AvancementsPhysiques.AnyAsync(a => a.Id != s.Id && a.ProjetId == projetId && a.PeriodeId == periodeId
                                                      && a.ContratId == s.ContratId && a.TypeTravauxId == typeTravauxId))
            return (false, "Un avancement est déjà saisi pour cette opération, ce mois, ce contrat et ce type de travaux : modifiez-le.");

        AvancementPhysique avancement;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            avancement = new AvancementPhysique { CreePar = acteurId };
            ctx.AvancementsPhysiques.Add(avancement);
        }
        else
        {
            var existant = await ctx.AvancementsPhysiques.Include(a => a.Periode).FirstOrDefaultAsync(a => a.Id == s.Id);
            if (existant is null) return (false, "Avancement introuvable.");
            if (existant.Periode.ExerciceId != periode.ExerciceId) return (false, "Un avancement ne change pas d'exercice.");
            // Déplacer un relevé exige aussi le périmètre de l'opération qu'il quitte.
            if (existant.ProjetId != projetId && await _acces.PerimetreProjetAsync(ctx, acteurId, existant.ProjetId) is { } refusOrigine)
                return (false, refusOrigine);
            avancement = existant;
            avant = new { avancement.KmRealises, avancement.TauxAvancement };
        }

        avancement.ProjetId = projetId;
        avancement.ContratId = s.ContratId;
        avancement.PeriodeId = periodeId;
        avancement.TypeTravauxId = typeTravauxId;
        avancement.KmRealises = s.KmRealises;
        avancement.TauxAvancement = s.TauxAvancement;
        avancement.Commentaire = commentaire;
        avancement.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesAxes.ModuleAudit,
            nameof(AvancementPhysique), avancement.Id.ToString(), avant, new { avancement.ProjetId, avancement.KmRealises, avancement.TauxAvancement });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid avancementId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var avancement = await ctx.AvancementsPhysiques.Include(a => a.Periode).Include(a => a.Projet).ThenInclude(p => p.Axe)
            .FirstOrDefaultAsync(a => a.Id == avancementId);
        if (avancement is null || AxesTravaux.FirstOrDefault(a => a.Code == avancement.Projet.Axe.Code) is not { } axe) return (false, "Avancement introuvable.");
        var acteurId = await _acces.SaisisseurAsync(axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(axe));
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, avancement.Periode.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        if (avancement.Periode.EstCloturee) return (false, "Ce mois est clos à la saisie.");
        var refus = await _acces.PerimetreAsync(acteurId,
            new CiblePerimetre(AxeId: avancement.Projet.AxeId, StructureId: avancement.Projet.StructureId, SiteId: avancement.Projet.SiteId));
        if (refus is not null) return (false, refus);

        avancement.EstSupprime = true;
        avancement.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesAxes.ModuleAudit, nameof(AvancementPhysique),
            avancement.Id.ToString(), new { avancement.ProjetId, avancement.KmRealises }, null);
        return (true, null);
    }
}
