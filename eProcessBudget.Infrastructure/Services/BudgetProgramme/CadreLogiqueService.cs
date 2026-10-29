using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

public sealed record ElementCadreLogique(
    Ulid Id, TypeElementCL Type, Ulid? ActionId, string? Action, Ulid? ParentId, string Libelle,
    Ulid? IndicateurId, string? Indicateur, Ulid? SourceVerificationId, string? SourceVerification,
    decimal? ValeurCible, int Ordre, IReadOnlyList<ElementListe> Risques);

public sealed record CadreLogique(Ulid ProgrammeId, Ulid CycleId, bool EstModifiable,
                                  IReadOnlyList<ElementCadreLogique> Elements, IReadOnlyList<string> Anomalies);

/// <summary>Saisie d'un élément du cadre logique (création si Id vide).</summary>
public sealed class SaisieElementCadre
{
    public Ulid Id { get; set; }
    public Ulid ProgrammeId { get; set; }
    public Ulid CycleId { get; set; }
    public TypeElementCL Type { get; set; } = TypeElementCL.ResultatImmediat;
    public Ulid? ActionId { get; set; }
    public Ulid? ParentId { get; set; }
    public string Libelle { get; set; } = string.Empty;
    public Ulid? IndicateurId { get; set; }
    public Ulid? SourceVerificationId { get; set; }
    public decimal? ValeurCible { get; set; }
    public int Ordre { get; set; }
}

/// <summary>
/// Cadre logique d'un programme pour un cycle (cahier fonctionnel § 3.3.4) :
///   objectif général (un) › objectifs spécifiques (un par action) › résultats immédiats,
/// chacun avec son indicateur, sa source de vérification et ses risques (catalogue).
/// Règle § 4.2 : chaque objectif spécifique a au moins un indicateur et une source de
/// vérification — vérifiée ici (anomalies) et à la validation du cycle.
/// </summary>
public interface ICadreLogiqueService
{
    Task<CadreLogique?> ObtenirAsync(Ulid programmeId, Ulid cycleId);

    /// <summary>Crée l'objectif général et un objectif spécifique par action active, à partir du programme.</summary>
    Task<(bool Ok, string? Erreur)> InitialiserAsync(Ulid programmeId, Ulid cycleId);

    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieElementCadre saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid elementId);
    Task<(bool Ok, string? Erreur)> DefinirRisquesAsync(Ulid elementId, IReadOnlyCollection<Ulid> risques);
}

public sealed class CadreLogiqueService : ICadreLogiqueService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesBudgetProgramme _acces;
    private readonly IAuditService _audit;

    public CadreLogiqueService(IDbContextFactory<AppDbContext> factory, AccesBudgetProgramme acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<CadreLogique?> ObtenirAsync(Ulid programmeId, Ulid cycleId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        var statut = await ctx.CyclesProgrammation.Where(c => c.Id == cycleId).Select(c => (StatutCycle?)c.Statut).FirstOrDefaultAsync();
        if (statut is null || !await ctx.Programmes.AnyAsync(p => p.Id == programmeId)) return null;

        var elements = await ctx.CadreLogiqueElements.AsNoTracking()
            .Where(e => e.ProgrammeId == programmeId && e.CycleProgrammationId == cycleId)
            .OrderBy(e => e.TypeElement).ThenBy(e => e.Ordre)
            .Select(e => new ElementCadreLogique(
                e.Id, e.TypeElement, e.ActionId, e.Action == null ? null : e.Action.Code + " — " + e.Action.Libelle,
                e.ElementParentId, e.Libelle,
                e.IndicateurId, e.Indicateur == null ? null : e.Indicateur.Code + " — " + e.Indicateur.Libelle,
                e.SourceVerificationId, e.SourceVerification == null ? null : e.SourceVerification.Libelle,
                e.ValeurCible, e.Ordre,
                e.Risques.OrderBy(r => r.Risque.Code).Select(r => new ElementListe(r.RisqueId, r.Risque.Code + " — " + r.Risque.Libelle)).ToList()))
            .ToListAsync();

        var anomalies = await AnomaliesAsync(ctx, programmeId, cycleId);
        return new CadreLogique(programmeId, cycleId, statut == StatutCycle.Brouillon, elements, anomalies);
    }

    public async Task<(bool Ok, string? Erreur)> InitialiserAsync(Ulid programmeId, Ulid cycleId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.BudgetProgrammeDefinir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir le cadre logique"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var (_, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, cycleId);
        if (erreur is not null) return (false, erreur);

        var programme = await ctx.Programmes.AsNoTracking().Include(p => p.Actions).FirstOrDefaultAsync(p => p.Id == programmeId);
        if (programme is null) return (false, "Programme introuvable.");

        var existants = await ctx.CadreLogiqueElements.Where(e => e.ProgrammeId == programmeId && e.CycleProgrammationId == cycleId).ToListAsync();
        var og = existants.FirstOrDefault(e => e.TypeElement == TypeElementCL.ObjectifGeneral);
        if (og is null)
        {
            og = new CadreLogiqueElement
            {
                ProgrammeId = programmeId, CycleProgrammationId = cycleId, TypeElement = TypeElementCL.ObjectifGeneral,
                Libelle = programme.ObjectifGeneral, CreePar = acteurId
            };
            ctx.CadreLogiqueElements.Add(og);
        }

        var ordre = existants.Where(e => e.TypeElement == TypeElementCL.ObjectifSpecifique).Select(e => e.Ordre).DefaultIfEmpty().Max();
        foreach (var action in programme.Actions.Where(a => a.EstActif).OrderBy(a => a.Ordre).ThenBy(a => a.Code))
        {
            if (existants.Any(e => e.TypeElement == TypeElementCL.ObjectifSpecifique && e.ActionId == action.Id)) continue;
            ctx.CadreLogiqueElements.Add(new CadreLogiqueElement
            {
                ProgrammeId = programmeId, CycleProgrammationId = cycleId, TypeElement = TypeElementCL.ObjectifSpecifique,
                ActionId = action.Id, ElementParent = og, Libelle = action.ObjectifSpecifique, Ordre = ++ordre, CreePar = acteurId
            });
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "INITIALISATION_CADRE_LOGIQUE", AccesBudgetProgramme.ModuleAudit, nameof(Programme),
            programmeId.ToString(), null, new { Cycle = cycleId.ToString() });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieElementCadre s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.BudgetProgrammeDefinir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir le cadre logique"));

        s.Libelle = (s.Libelle ?? "").Trim();
        if (s.Libelle.Length == 0) return (false, "Le libellé est obligatoire.");
        if (s.Libelle.Length > 500) return (false, "Le libellé ne doit pas dépasser 500 caractères.");
        if (!Enum.IsDefined(s.Type)) return (false, "Type d'élément inconnu.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var (_, erreurCycle) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, s.CycleId);
        if (erreurCycle is not null) return (false, erreurCycle);
        if (!await ctx.Programmes.AnyAsync(p => p.Id == s.ProgrammeId)) return (false, "Programme introuvable.");

        var freres = ctx.CadreLogiqueElements.Where(e => e.ProgrammeId == s.ProgrammeId && e.CycleProgrammationId == s.CycleId && e.Id != s.Id);

        // ── Place dans la logique d'intervention ────────────────────────────
        switch (s.Type)
        {
            case TypeElementCL.ObjectifGeneral:
                s.ActionId = null; s.ParentId = null; s.ValeurCible = null;
                if (await freres.AnyAsync(e => e.TypeElement == TypeElementCL.ObjectifGeneral))
                    return (false, "Le programme a déjà son objectif général pour ce cycle.");
                break;

            case TypeElementCL.ObjectifSpecifique:
                s.ValeurCible = null;
                if (s.ActionId is null || !await ctx.Actions.AnyAsync(a => a.Id == s.ActionId && a.ProgrammeId == s.ProgrammeId))
                    return (false, "Un objectif spécifique correspond à une action du programme.");
                if (await freres.AnyAsync(e => e.TypeElement == TypeElementCL.ObjectifSpecifique && e.ActionId == s.ActionId))
                    return (false, "Cette action a déjà son objectif spécifique.");
                s.ParentId = await freres.Where(e => e.TypeElement == TypeElementCL.ObjectifGeneral).Select(e => (Ulid?)e.Id).FirstOrDefaultAsync();
                break;

            case TypeElementCL.ResultatImmediat:
                var parent = await freres.Where(e => e.Id == s.ParentId && e.TypeElement == TypeElementCL.ObjectifSpecifique)
                                         .Select(e => new { e.ActionId }).FirstOrDefaultAsync();
                if (parent is null) return (false, "Un résultat immédiat se rattache à un objectif spécifique du programme.");
                s.ActionId = parent.ActionId;
                if (s.ValeurCible is { } v && decimal.Round(v, 4) != v) return (false, "La valeur cible compte 4 décimales au plus.");
                break;
        }

        // ── Indicateur du bon niveau, source de vérification de la bonne liste ──
        if (s.IndicateurId is { } indicateurId)
        {
            var ind = await ctx.Indicateurs.AsNoTracking().Where(i => i.Id == indicateurId)
                               .Select(i => new { i.ProgrammeId, i.ActionId }).FirstOrDefaultAsync();
            var coherent = ind is not null && (s.Type == TypeElementCL.ObjectifGeneral
                ? ind.ProgrammeId == s.ProgrammeId
                : ind.ActionId == s.ActionId);
            if (!coherent)
                return (false, s.Type == TypeElementCL.ObjectifGeneral
                    ? "L'indicateur de l'objectif général est un indicateur de ce programme."
                    : "L'indicateur doit être rattaché à l'action de l'objectif.");
        }
        if (s.SourceVerificationId is { } sourceId
            && !await ctx.ListesValeurs.AnyAsync(l => l.Id == sourceId && l.Categorie == CategoriesListeValeur.SourceVerification))
            return (false, "Source de vérification introuvable.");

        CadreLogiqueElement element;
        if (s.Id == Ulid.Empty)
        {
            element = new CadreLogiqueElement { ProgrammeId = s.ProgrammeId, CycleProgrammationId = s.CycleId, TypeElement = s.Type, CreePar = acteurId };
            ctx.CadreLogiqueElements.Add(element);
        }
        else
        {
            var existant = await ctx.CadreLogiqueElements.FirstOrDefaultAsync(e => e.Id == s.Id);
            if (existant is null || existant.ProgrammeId != s.ProgrammeId || existant.TypeElement != s.Type)
                return (false, "Élément introuvable.");
            element = existant;
            element.ModifiePar = acteurId;
        }

        element.ActionId = s.ActionId; element.ElementParentId = s.ParentId; element.Libelle = s.Libelle;
        element.IndicateurId = s.IndicateurId; element.SourceVerificationId = s.SourceVerificationId;
        element.ValeurCible = s.ValeurCible; element.Ordre = s.Ordre;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, s.Id == Ulid.Empty ? "CREATION" : "MODIFICATION", AccesBudgetProgramme.ModuleAudit,
            nameof(CadreLogiqueElement), element.Id.ToString(), null, new { Type = s.Type.ToString(), s.Libelle });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid elementId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.BudgetProgrammeDefinir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir le cadre logique"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var element = await ctx.CadreLogiqueElements.Include(e => e.Risques).FirstOrDefaultAsync(e => e.Id == elementId);
        if (element is null) return (false, "Élément introuvable.");
        var (_, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, element.CycleProgrammationId);
        if (erreur is not null) return (false, erreur);
        if (await ctx.CadreLogiqueElements.AnyAsync(e => e.ElementParentId == elementId))
            return (false, "Supprimez d'abord les éléments rattachés à celui-ci.");

        ctx.CadreLogiqueRisques.RemoveRange(element.Risques);
        element.EstSupprime = true;
        element.DateSuppression = DateTime.UtcNow;
        element.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION", AccesBudgetProgramme.ModuleAudit, nameof(CadreLogiqueElement),
            elementId.ToString(), new { Type = element.TypeElement.ToString(), element.Libelle }, null);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> DefinirRisquesAsync(Ulid elementId, IReadOnlyCollection<Ulid> risques)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.BudgetProgrammeDefinir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir le cadre logique"));

        var cibles = risques.Distinct().ToList();
        await using var ctx = await _factory.CreateDbContextAsync();
        var element = await ctx.CadreLogiqueElements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == elementId);
        if (element is null) return (false, "Élément introuvable.");
        var (_, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, element.CycleProgrammationId);
        if (erreur is not null) return (false, erreur);

        var actuels = await ctx.CadreLogiqueRisques.Where(r => r.CadreLogiqueElementId == elementId).ToListAsync();
        var dejaLies = actuels.Select(r => r.RisqueId).ToHashSet();
        var nouveaux = cibles.Where(r => !dejaLies.Contains(r)).ToList();
        if (await ctx.Risques.CountAsync(r => nouveaux.Contains(r.Id) && r.EstActif) != nouveaux.Count)
            return (false, "Un des risques choisis est introuvable ou inactif.");

        ctx.CadreLogiqueRisques.RemoveRange(actuels.Where(r => !cibles.Contains(r.RisqueId)));
        foreach (var risqueId in nouveaux)
            ctx.CadreLogiqueRisques.Add(new CadreLogiqueRisque { CadreLogiqueElementId = elementId, RisqueId = risqueId, CreePar = acteurId });
        await ctx.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Ce qui manque au cadre logique d'un programme (règle § 4.2). Utilisé par l'éditeur et
    /// par la validation du cycle (<paramref name="programmeId"/> null : tous les programmes
    /// dotés d'un cadre logique sur ce cycle).
    /// </summary>
    internal static async Task<List<string>> AnomaliesAsync(AppDbContext ctx, Ulid? programmeId, Ulid cycleId)
    {
        var q = ctx.CadreLogiqueElements.AsNoTracking().Where(e => e.CycleProgrammationId == cycleId);
        if (programmeId is { } p) q = q.Where(e => e.ProgrammeId == p);

        var elements = await q.Select(e => new
        {
            e.ProgrammeId, Programme = e.Programme.Code, e.TypeElement, e.ActionId, Action = e.Action == null ? null : e.Action.Code,
            e.IndicateurId, e.SourceVerificationId, e.ValeurCible
        }).ToListAsync();

        var anomalies = new List<string>();
        foreach (var prog in elements.GroupBy(e => (e.ProgrammeId, e.Programme)))
        {
            if (!prog.Any(e => e.TypeElement == TypeElementCL.ObjectifGeneral))
                anomalies.Add($"{prog.Key.Programme} : objectif général manquant.");
            foreach (var os in prog.Where(e => e.TypeElement == TypeElementCL.ObjectifSpecifique))
            {
                if (os.IndicateurId is null) anomalies.Add($"{prog.Key.Programme} › {os.Action} : objectif spécifique sans indicateur.");
                if (os.SourceVerificationId is null) anomalies.Add($"{prog.Key.Programme} › {os.Action} : objectif spécifique sans source de vérification.");
            }
            foreach (var ri in prog.Where(e => e.TypeElement == TypeElementCL.ResultatImmediat && e.ValeurCible is null))
                anomalies.Add($"{prog.Key.Programme} › {ri.Action} : résultat immédiat sans valeur cible.");
        }

        if (programmeId is { } pid)
        {
            var actionsSansObjectif = await ctx.Actions.AsNoTracking()
                .Where(a => a.ProgrammeId == pid && a.EstActif)
                .Where(a => !ctx.CadreLogiqueElements.Any(e => e.CycleProgrammationId == cycleId && e.ActionId == a.Id
                                                            && e.TypeElement == TypeElementCL.ObjectifSpecifique))
                .Select(a => a.Code).ToListAsync();
            anomalies.AddRange(actionsSansObjectif.Select(c => $"Action {c} : pas d'objectif spécifique."));
        }
        return anomalies;
    }
}
