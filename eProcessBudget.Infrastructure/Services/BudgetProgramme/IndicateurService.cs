using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

/// <summary>Ligne du tableau des indicateurs principaux assortis de cibles (§ 3.3.3).</summary>
public sealed record LigneTableauIndicateur(
    Ulid Id, NiveauIndicateur Niveau, string? Programme, string? Action, string Code, string Libelle, string Unite,
    SensEvolution Sens, int? AnneeReference, decimal? ValeurReference, IReadOnlyDictionary<int, decimal> Cibles, bool EstActif)
{
    public bool EstComplet(IEnumerable<int> annees) => ValeurReference is not null && annees.All(Cibles.ContainsKey);
}

/// <summary>Ligne de la grille de saisie des réalisations d'une période.</summary>
public sealed record LigneRealisation(
    Ulid IndicateurId, string Code, string Libelle, NiveauIndicateur Niveau, string? Programme, string? Action,
    string Unite, SensEvolution Sens, decimal? Cible,
    Ulid? RealisationId, decimal? Valeur, string? Commentaire, string? Justification, StatutSaisie? Statut, Ulid? TransmisParId)
{
    public decimal? TauxAtteinte => Valeur is { } v ? CalculsBudgetProgramme.TauxAtteinte(v, Cible, Sens) : null;
    public bool? EstAtteinte => Valeur is { } v ? CalculsBudgetProgramme.EstAtteinte(v, Cible, Sens) : null;
}

/// <summary>Saisie d'une réalisation.</summary>
public sealed class SaisieRealisation
{
    public Ulid IndicateurId { get; set; }
    public Ulid PeriodeId { get; set; }
    public decimal? Valeur { get; set; }
    public string? Commentaire { get; set; }
    public string? Justification { get; set; }
    /// <summary>Responsable ayant transmis la donnée au service financier.</summary>
    public Ulid? TransmisParId { get; set; }
}

/// <summary>Synthèse de la performance d'un programme (taux d'atteinte des cibles, § 3.3.9).</summary>
public sealed record SynthesePerformance(string Programme, string Libelle, int NbIndicateurs, int NbMesures, int NbAtteints, decimal? TauxAtteinteMoyen);

/// <summary>
/// Indicateurs et performance (cahier fonctionnel § 3.3.3 et 3.3.7) : cibles annuelles du
/// cycle, tableau des indicateurs, saisie périodique des réalisations par le service
/// financier (sur transmission des responsables), validation, taux d'atteinte.
/// </summary>
public interface IIndicateurService
{
    /// <summary>Cibles de l'indicateur pour chaque année du cycle (N à N+2).</summary>
    Task<IReadOnlyDictionary<int, decimal>> ObtenirCiblesAsync(Ulid indicateurId, Ulid cycleId);

    /// <summary>Remplace les cibles du cycle (valeur null = pas de cible). Cycle en brouillon.</summary>
    Task<(bool Ok, string? Erreur)> DefinirCiblesAsync(Ulid indicateurId, Ulid cycleId, IReadOnlyDictionary<int, decimal?> cibles);

    Task<List<LigneTableauIndicateur>> TableauAsync(Ulid cycleId, Ulid? programmeId = null);

    Task<List<LigneRealisation>> ListerRealisationsAsync(Ulid periodeId, Ulid? programmeId = null);
    Task<(bool Ok, string? Erreur)> EnregistrerRealisationAsync(SaisieRealisation saisie);
    Task<(bool Ok, string? Erreur)> ValiderRealisationAsync(Ulid realisationId);
    Task<(bool Ok, string? Erreur)> RouvrirRealisationAsync(Ulid realisationId);

    /// <summary>Taux d'atteinte par programme, sur les réalisations validées d'un exercice.</summary>
    Task<List<SynthesePerformance>> SyntheseAsync(Ulid exerciceId);
}

public sealed class IndicateurService : IIndicateurService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesBudgetProgramme _acces;
    private readonly IAuditService _audit;

    public IndicateurService(IDbContextFactory<AppDbContext> factory, AccesBudgetProgramme acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    // ═════ Cibles ═════════════════════════════════════════════════════════════

    public async Task<IReadOnlyDictionary<int, decimal>> ObtenirCiblesAsync(Ulid indicateurId, Ulid cycleId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return new Dictionary<int, decimal>();

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.IndicateurCibles.AsNoTracking()
            .Where(c => c.IndicateurId == indicateurId && c.CycleProgrammationId == cycleId)
            .ToDictionaryAsync(c => c.Annee, c => c.ValeurCible);
    }

    public async Task<(bool Ok, string? Erreur)> DefinirCiblesAsync(Ulid indicateurId, Ulid cycleId, IReadOnlyDictionary<int, decimal?> cibles)
    {
        // Les cibles sont saisies par l'Administrateur ou le Responsable Budget (§ 3.3.1).
        var acteurId = await _acces.ActeurAsync(TypeDroit.BudgetProgrammeDefinir, TypeDroit.ProgrammationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir les cibles"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var (cycle, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, cycleId);
        if (erreur is not null) return (false, erreur);
        if (!await ctx.Indicateurs.AnyAsync(i => i.Id == indicateurId)) return (false, "Indicateur introuvable.");

        var annees = Enumerable.Range(cycle!.AnneeBase, cycle.AnneeFin - cycle.AnneeBase + 1).ToHashSet();
        if (cibles.Keys.Any(a => !annees.Contains(a))) return (false, $"Les cibles portent sur les années {cycle.AnneeBase} à {cycle.AnneeFin}.");
        if (cibles.Values.Any(v => v is { } d && decimal.Round(d, 4) != d)) return (false, "Une cible compte 4 décimales au plus.");

        var actuelles = await ctx.IndicateurCibles.Where(c => c.IndicateurId == indicateurId && c.CycleProgrammationId == cycleId).ToListAsync();
        var avant = actuelles.ToDictionary(c => c.Annee, c => c.ValeurCible);

        foreach (var (annee, valeur) in cibles)
        {
            var existante = actuelles.FirstOrDefault(c => c.Annee == annee);
            if (valeur is null)
            {
                if (existante is not null) { existante.EstSupprime = true; existante.DateSuppression = DateTime.UtcNow; existante.SupprimePar = acteurId; }
            }
            else if (existante is null)
                ctx.IndicateurCibles.Add(new IndicateurCible { IndicateurId = indicateurId, CycleProgrammationId = cycleId, Annee = annee, ValeurCible = valeur.Value, CreePar = acteurId });
            else if (existante.ValeurCible != valeur.Value)
            {
                existante.ValeurCible = valeur.Value;
                existante.ModifiePar = acteurId;
            }
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CIBLES", AccesBudgetProgramme.ModuleAudit, nameof(Indicateur),
            indicateurId.ToString(), avant, cibles);
        return (true, null);
    }

    public async Task<List<LigneTableauIndicateur>> TableauAsync(Ulid cycleId, Ulid? programmeId = null)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Indicateurs.AsNoTracking();
        if (programmeId is { } p) q = q.Where(i => i.ProgrammeId == p || (i.Action != null && i.Action.ProgrammeId == p));

        var lignes = await q
            .OrderBy(i => i.Programme != null ? i.Programme.Ordre : i.Action!.Programme.Ordre)
            .ThenBy(i => i.Niveau).ThenBy(i => i.Code)
            .Select(i => new
            {
                i.Id, i.Niveau, i.Code, i.Libelle, i.SensEvolution, i.AnneeReference, i.ValeurReference, i.EstActif,
                Programme = i.Programme != null ? i.Programme.Code : i.Action!.Programme.Code,
                Action = i.Action != null ? i.Action.Code : null,
                Unite = i.UniteMesure.Symbole ?? i.UniteMesure.Libelle,
                Cibles = i.Cibles.Where(c => c.CycleProgrammationId == cycleId).Select(c => new { c.Annee, c.ValeurCible }).ToList()
            })
            .ToListAsync();

        return lignes.Select(l => new LigneTableauIndicateur(l.Id, l.Niveau, l.Programme, l.Action, l.Code, l.Libelle, l.Unite,
            l.SensEvolution, l.AnneeReference, l.ValeurReference, l.Cibles.ToDictionary(c => c.Annee, c => c.ValeurCible), l.EstActif)).ToList();
    }

    // ═════ Réalisations ═══════════════════════════════════════════════════════

    public async Task<List<LigneRealisation>> ListerRealisationsAsync(Ulid periodeId, Ulid? programmeId = null)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var annee = await ctx.Periodes.Where(p => p.Id == periodeId).Select(p => (int?)p.Exercice.Annee).FirstOrDefaultAsync();
        if (annee is null) return [];

        var q = ctx.Indicateurs.AsNoTracking().Where(i => i.EstActif);
        if (programmeId is { } prog) q = q.Where(i => i.ProgrammeId == prog || (i.Action != null && i.Action.ProgrammeId == prog));

        var lignes = await q
            .OrderBy(i => i.Programme != null ? i.Programme.Ordre : i.Action!.Programme.Ordre).ThenBy(i => i.Code)
            .Select(i => new
            {
                i.Id, i.Code, i.Libelle, i.Niveau, i.SensEvolution,
                Programme = i.Programme != null ? i.Programme.Code : i.Action!.Programme.Code,
                Action = i.Action != null ? i.Action.Code : null,
                Unite = i.UniteMesure.Symbole ?? i.UniteMesure.Libelle,
                // Cible de l'année : celle du cycle le plus récent qui la couvre (même règle que la vue).
                Cible = i.Cibles.Where(c => c.Annee == annee).OrderByDescending(c => c.CycleProgrammation.AnneeBase)
                                .Select(c => (decimal?)c.ValeurCible).FirstOrDefault(),
                Realisation = i.Realisations.Where(r => r.PeriodeId == periodeId)
                    .Select(r => new { r.Id, r.ValeurRealisee, r.Commentaire, r.JustificationEcart, r.Statut, r.TransmisParId })
                    .FirstOrDefault()
            })
            .ToListAsync();

        return lignes.Select(l => new LigneRealisation(l.Id, l.Code, l.Libelle, l.Niveau, l.Programme, l.Action, l.Unite, l.SensEvolution,
            l.Cible, l.Realisation?.Id, l.Realisation?.ValeurRealisee, l.Realisation?.Commentaire, l.Realisation?.JustificationEcart,
            l.Realisation?.Statut, l.Realisation?.TransmisParId)).ToList();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerRealisationAsync(SaisieRealisation s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.IndicateurSaisirRealisation);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir les réalisations des indicateurs"));

        if (s.Valeur is null) return (false, "Saisissez la valeur réalisée.");
        if (decimal.Round(s.Valeur.Value, 4) != s.Valeur.Value) return (false, "La valeur compte 4 décimales au plus.");
        s.Commentaire = string.IsNullOrWhiteSpace(s.Commentaire) ? null : s.Commentaire.Trim();
        s.Justification = string.IsNullOrWhiteSpace(s.Justification) ? null : s.Justification.Trim();
        if (s.Commentaire?.Length > 1000 || s.Justification?.Length > 1000) return (false, "Commentaire et justification : 1 000 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var (periode, erreurPeriode) = await PeriodeSaisissableAsync(ctx, s.PeriodeId);
        if (erreurPeriode is not null) return (false, erreurPeriode);

        var indicateur = await ctx.Indicateurs.AsNoTracking()
            .Where(i => i.Id == s.IndicateurId && i.EstActif)
            .Select(i => new
            {
                i.Id, i.SensEvolution, i.StructureId, i.ActionId,
                ProgrammeId = i.ProgrammeId ?? i.Action!.ProgrammeId,
                Cible = i.Cibles.Where(c => c.Annee == periode!.Exercice.Annee).OrderByDescending(c => c.CycleProgrammation.AnneeBase)
                                .Select(c => (decimal?)c.ValeurCible).FirstOrDefault()
            })
            .FirstOrDefaultAsync();
        if (indicateur is null) return (false, "Indicateur introuvable ou inactif.");

        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(indicateur.ProgrammeId, indicateur.ActionId, StructureId: indicateur.StructureId));
        if (refus is not null) return (false, refus);

        // Un écart à la cible se justifie (§ 3.3.7).
        if (CalculsBudgetProgramme.EstAtteinte(s.Valeur.Value, indicateur.Cible, indicateur.SensEvolution) == false && s.Justification is null)
            return (false, "Cible non atteinte : la justification de l'écart est obligatoire.");

        if (s.TransmisParId is { } transmetteur && !await ctx.Utilisateurs.AnyAsync(u => u.Id == transmetteur && u.OrganismeId == ctx.OrganismeCourantId))
            return (false, "Le responsable qui transmet la donnée doit être un compte de l'organisme.");

        var realisation = await ctx.IndicateurRealisations.FirstOrDefaultAsync(r => r.IndicateurId == s.IndicateurId && r.PeriodeId == s.PeriodeId);
        if (realisation?.Statut == StatutSaisie.Valide) return (false, "Cette réalisation est validée : rouvrez-la pour la corriger.");

        var avant = realisation is null ? null : new { realisation.ValeurRealisee, realisation.Statut };
        if (realisation is null)
        {
            realisation = new IndicateurRealisation { IndicateurId = s.IndicateurId, PeriodeId = s.PeriodeId, CreePar = acteurId };
            ctx.IndicateurRealisations.Add(realisation);
        }
        realisation.ValeurRealisee = s.Valeur.Value;
        realisation.Commentaire = s.Commentaire;
        realisation.JustificationEcart = s.Justification;
        realisation.TransmisParId = s.TransmisParId;
        realisation.Statut = StatutSaisie.Brouillon;
        realisation.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesBudgetProgramme.ModuleAudit,
            nameof(IndicateurRealisation), realisation.Id.ToString(), avant, new { realisation.ValeurRealisee });
        return (true, null);
    }

    public Task<(bool Ok, string? Erreur)> ValiderRealisationAsync(Ulid realisationId)
        => ChangerStatutAsync(realisationId, StatutSaisie.Valide, "VALIDATION");

    public Task<(bool Ok, string? Erreur)> RouvrirRealisationAsync(Ulid realisationId)
        => ChangerStatutAsync(realisationId, StatutSaisie.Brouillon, "REOUVERTURE");

    private async Task<(bool Ok, string? Erreur)> ChangerStatutAsync(Ulid realisationId, StatutSaisie cible, string action)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.IndicateurSaisirRealisation);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("valider les réalisations"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var r = await ctx.IndicateurRealisations.Include(x => x.Indicateur).ThenInclude(i => i.Action)
                         .FirstOrDefaultAsync(x => x.Id == realisationId);
        if (r is null) return (false, "Réalisation introuvable.");

        var (_, erreurPeriode) = await PeriodeSaisissableAsync(ctx, r.PeriodeId);
        if (erreurPeriode is not null) return (false, erreurPeriode);

        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(r.Indicateur.ProgrammeId ?? r.Indicateur.Action?.ProgrammeId,
            r.Indicateur.ActionId, StructureId: r.Indicateur.StructureId));
        if (refus is not null) return (false, refus);

        if (r.Statut == cible) return (true, null);
        r.Statut = cible;
        r.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, action, AccesBudgetProgramme.ModuleAudit, nameof(IndicateurRealisation),
            r.Id.ToString(), null, new { r.ValeurRealisee, Statut = cible.ToString() });
        return (true, null);
    }

    public async Task<List<SynthesePerformance>> SyntheseAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await SyntheseAsync(ctx, exerciceId, null);
    }

    /// <summary>
    /// Synthèse de la performance par programme ; <paramref name="programmes"/> restreint aux
    /// programmes indiqués (périmètre de lecture du tableau de bord), null pour tous.
    /// </summary>
    internal static async Task<List<SynthesePerformance>> SyntheseAsync(AppDbContext ctx, Ulid exerciceId, IReadOnlyCollection<Ulid>? programmes)
    {
        var annee = await ctx.Exercices.Where(e => e.Id == exerciceId).Select(e => (int?)e.Annee).FirstOrDefaultAsync();
        if (annee is null) return [];

        // Dernière réalisation validée de chaque indicateur sur l'exercice (période la plus avancée).
        var mesures = await ctx.IndicateurRealisations.AsNoTracking()
            .Where(r => r.Periode.ExerciceId == exerciceId && r.Statut == StatutSaisie.Valide)
            .Select(r => new
            {
                r.IndicateurId, r.ValeurRealisee, r.Periode.DateFin, r.Indicateur.SensEvolution,
                ProgrammeId = r.Indicateur.ProgrammeId ?? r.Indicateur.Action!.ProgrammeId,
                Cible = r.Indicateur.Cibles.Where(c => c.Annee == annee).OrderByDescending(c => c.CycleProgrammation.AnneeBase)
                                           .Select(c => (decimal?)c.ValeurCible).FirstOrDefault()
            })
            .ToListAsync();

        var retenus = programmes?.ToList();
        var listeProgrammes = await ctx.Programmes.AsNoTracking()
            .Where(p => retenus == null || retenus.Contains(p.Id))
            .OrderBy(p => p.Ordre).ThenBy(p => p.Code)
            .Select(p => new
            {
                p.Id, p.Code, p.Libelle,
                NbIndicateurs = ctx.Indicateurs.Count(i => i.EstActif && (i.ProgrammeId == p.Id || (i.Action != null && i.Action.ProgrammeId == p.Id)))
            })
            .ToListAsync();

        return listeProgrammes.Select(p =>
        {
            var dernieres = mesures.Where(m => m.ProgrammeId == p.Id)
                                   .GroupBy(m => m.IndicateurId)
                                   .Select(g => g.OrderByDescending(m => m.DateFin).First())
                                   .ToList();
            var taux = dernieres.Select(m => CalculsBudgetProgramme.TauxAtteinte(m.ValeurRealisee, m.Cible, m.SensEvolution))
                                .OfType<decimal>().ToList();
            var atteints = dernieres.Count(m => CalculsBudgetProgramme.EstAtteinte(m.ValeurRealisee, m.Cible, m.SensEvolution) == true);
            return new SynthesePerformance(p.Code, p.Libelle, p.NbIndicateurs, dernieres.Count, atteints,
                taux.Count == 0 ? null : Math.Round(taux.Average(), 2));
        }).ToList();
    }

    /// <summary>La période appartient à un exercice ouvert et n'est pas close à la saisie (§ 4.2).</summary>
    private static async Task<(Periode? Periode, string? Erreur)> PeriodeSaisissableAsync(AppDbContext ctx, Ulid periodeId)
    {
        var periode = await ctx.Periodes.AsNoTracking().Include(p => p.Exercice).FirstOrDefaultAsync(p => p.Id == periodeId);
        if (periode is null) return (null, "Période introuvable.");
        if (periode.Exercice.Statut != StatutExercice.Ouvert) return (periode, "La saisie exige un exercice ouvert.");
        if (periode.EstCloturee) return (periode, "Cette période est close à la saisie.");
        return (periode, null);
    }
}
