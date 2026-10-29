using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

/// <summary>État des clés d'une ligne pour un exercice.</summary>
public sealed record EtatClesLigne(Ulid LigneId, string Code, string Libelle, int NbActivites, decimal Total)
{
    public bool EstComplete => Total == 100m;
}

public sealed record PartRepartition(Ulid ActiviteId, string Activite, decimal Pourcentage);

/// <summary>Part du temps d'un agent sur une action (et une activité).</summary>
public sealed record PartTemps(Ulid ActionId, string Action, Ulid? ActiviteId, string? Activite, decimal Pourcentage);

public sealed record EtatTempsAgent(Ulid AgentId, string Matricule, string Nom, string Structure, decimal Total);

/// <summary>Charges ventilées sur une action (§ 3.3.6).</summary>
public sealed record LigneVentilation(string Programme, string Action, string Libelle, decimal PartTemps, decimal Personnel, decimal Communes)
{
    public decimal Total => Personnel + Communes;
}

/// <summary>
/// Répartitions (cahier fonctionnel § 3.2.4 et 3.3.6) :
///   - clés de répartition d'une ligne budgétaire entre activités, pour un exercice : la
///     somme vaut exactement 100 % (règle § 4.3) ;
///   - répartition du temps de chaque agent entre actions et activités : au plus 100 % ;
///   - ventilation des charges de personnel et des charges communes au prorata du temps.
/// </summary>
public interface IRepartitionService
{
    Task<(List<EtatClesLigne> Items, int Total)> ListerLignesAsync(Ulid exerciceId, string? recherche, int page = 1, int taille = 20);
    Task<List<PartRepartition>> ObtenirClesAsync(Ulid exerciceId, Ulid ligneId);

    /// <summary>Remplace les clés d'une ligne : liste vide = aucune répartition, sinon total = 100 %.</summary>
    Task<(bool Ok, string? Erreur)> DefinirClesAsync(Ulid exerciceId, Ulid ligneId, IReadOnlyList<(Ulid ActiviteId, decimal Pourcentage)> parts);

    Task<(List<EtatTempsAgent> Items, int Total)> ListerAgentsAsync(Ulid exerciceId, string? recherche, int page = 1, int taille = 20);
    Task<List<PartTemps>> ObtenirTempsAsync(Ulid exerciceId, Ulid agentId);
    Task<(bool Ok, string? Erreur)> DefinirTempsAsync(Ulid exerciceId, Ulid agentId, IReadOnlyList<(Ulid ActionId, Ulid? ActiviteId, decimal Pourcentage)> parts);

    /// <summary>Ventile les charges de personnel et communes de l'exercice sur les actions, selon le temps des agents.</summary>
    Task<List<LigneVentilation>> VentilerAsync(Ulid exerciceId, decimal chargesPersonnel, decimal chargesCommunes);
}

public sealed class RepartitionService : IRepartitionService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesBudgetProgramme _acces;
    private readonly IAuditService _audit;

    public RepartitionService(IDbContextFactory<AppDbContext> factory, AccesBudgetProgramme acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    // ═════ Clés de répartition ════════════════════════════════════════════════

    public async Task<(List<EtatClesLigne> Items, int Total)> ListerLignesAsync(Ulid exerciceId, string? recherche, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.LignesBudgetaires.AsNoTracking().Where(l => l.EstActif && l.Sens == SensLigne.Depense);
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            q = q.Where(l => l.Code.Contains(r) || l.Libelle.Contains(r));
        }

        var total = await q.CountAsync();
        taille = Math.Clamp(taille, 1, 200);
        var items = await q.OrderBy(l => l.Code).Skip(Math.Max(0, (page - 1) * taille)).Take(taille)
            .Select(l => new EtatClesLigne(l.Id, l.Code, l.Libelle,
                ctx.ClesRepartition.Count(c => c.LigneBudgetaireId == l.Id && c.ExerciceId == exerciceId),
                ctx.ClesRepartition.Where(c => c.LigneBudgetaireId == l.Id && c.ExerciceId == exerciceId).Sum(c => (decimal?)c.Pourcentage) ?? 0))
            .ToListAsync();
        return (items, total);
    }

    public async Task<List<PartRepartition>> ObtenirClesAsync(Ulid exerciceId, Ulid ligneId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ClesRepartition.AsNoTracking()
            .Where(c => c.ExerciceId == exerciceId && c.LigneBudgetaireId == ligneId)
            .OrderBy(c => c.Activite.Code)
            .Select(c => new PartRepartition(c.ActiviteId, c.Activite.Code + " — " + c.Activite.Libelle, c.Pourcentage))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> DefinirClesAsync(Ulid exerciceId, Ulid ligneId, IReadOnlyList<(Ulid ActiviteId, decimal Pourcentage)> parts)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.ProgrammationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir les clés de répartition"));

        var erreurParts = ControlerCles(parts);
        if (erreurParts is not null) return (false, erreurParts);

        await using var ctx = await _factory.CreateDbContextAsync();
        var (_, erreurExercice) = await AccesBudgetProgramme.ExerciceSaisissableAsync(ctx, exerciceId, preparationAdmise: true);
        if (erreurExercice is not null) return (false, erreurExercice);

        var ligne = await ctx.LignesBudgetaires.AsNoTracking().Where(l => l.Id == ligneId)
            .Select(l => new { l.Id, l.Sens, AxeId = l.SousRubrique.Rubrique.AxeId }).FirstOrDefaultAsync();
        if (ligne is null) return (false, "Ligne budgétaire introuvable.");
        if (ligne.Sens != SensLigne.Depense) return (false, "Seules les lignes de dépense se répartissent entre activités.");

        var ids = parts.Select(p => p.ActiviteId).ToList();
        var activites = await ctx.Activites.AsNoTracking().Where(a => ids.Contains(a.Id) && a.EstActif)
            .Select(a => new { a.Id, a.ActionId, a.Action.ProgrammeId }).ToListAsync();
        if (activites.Count != ids.Count) return (false, "Une des activités est introuvable ou inactive.");

        // Périmètre : l'axe de la ligne, ou chacune des activités (programme ou action).
        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: ligne.AxeId));
        if (refus is not null)
        {
            refus = null;
            foreach (var a in activites)
                refus ??= await _acces.PerimetreAsync(acteurId, new CiblePerimetre(a.ProgrammeId, a.ActionId));
            if (refus is not null) return (false, refus);
        }

        var actuelles = await ctx.ClesRepartition.Where(c => c.ExerciceId == exerciceId && c.LigneBudgetaireId == ligneId).ToListAsync();
        var avant = actuelles.Select(c => new { Activite = c.ActiviteId.ToString(), c.Pourcentage }).ToList();
        foreach (var c in actuelles.Where(c => !ids.Contains(c.ActiviteId))) Supprimer(c, acteurId);
        foreach (var (activiteId, pourcentage) in parts)
        {
            var cle = actuelles.FirstOrDefault(c => c.ActiviteId == activiteId);
            if (cle is null)
                ctx.ClesRepartition.Add(new CleRepartition { ExerciceId = exerciceId, LigneBudgetaireId = ligneId, ActiviteId = activiteId, Pourcentage = pourcentage, CreePar = acteurId });
            else
            {
                cle.Pourcentage = pourcentage;
                cle.ModifiePar = acteurId;
            }
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CLES_REPARTITION", AccesBudgetProgramme.ModuleAudit, nameof(LigneBudgetaire),
            ligneId.ToString(), avant, parts.Select(p => new { Activite = p.ActiviteId.ToString(), p.Pourcentage }));
        return (true, null);
    }

    /// <summary>Parts dans ]0 ; 100], 4 décimales, activités distinctes, total exactement 100 % (ou aucune part).</summary>
    internal static string? ControlerCles(IReadOnlyList<(Ulid ActiviteId, decimal Pourcentage)> parts)
    {
        if (parts.Count == 0) return null;
        if (parts.Select(p => p.ActiviteId).Distinct().Count() != parts.Count) return "Une activité figure deux fois.";
        if (parts.Any(p => p.Pourcentage is <= 0 or > 100)) return "Chaque part est comprise entre 0 (exclu) et 100 %.";
        if (parts.Any(p => decimal.Round(p.Pourcentage, 4) != p.Pourcentage)) return "Une part compte 4 décimales au plus.";
        var total = parts.Sum(p => p.Pourcentage);
        return total == 100m ? null : $"La somme des clés doit faire exactement 100 % (actuellement {total:0.####} %).";
    }

    // ═════ Temps des agents ═══════════════════════════════════════════════════

    public async Task<(List<EtatTempsAgent> Items, int Total)> ListerAgentsAsync(Ulid exerciceId, string? recherche, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Agents.AsNoTracking().Where(a => a.EstActif);
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            q = q.Where(a => a.Matricule.Contains(r) || a.NomComplet.Contains(r));
        }
        var total = await q.CountAsync();
        taille = Math.Clamp(taille, 1, 200);
        var items = await q.OrderBy(a => a.NomComplet).Skip(Math.Max(0, (page - 1) * taille)).Take(taille)
            .Select(a => new EtatTempsAgent(a.Id, a.Matricule, a.NomComplet, a.Structure.Code,
                ctx.RepartitionsTemps.Where(t => t.AgentId == a.Id && t.ExerciceId == exerciceId).Sum(t => (decimal?)t.PourcentageTemps) ?? 0))
            .ToListAsync();
        return (items, total);
    }

    public async Task<List<PartTemps>> ObtenirTempsAsync(Ulid exerciceId, Ulid agentId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.RepartitionsTemps.AsNoTracking()
            .Where(t => t.ExerciceId == exerciceId && t.AgentId == agentId)
            .OrderBy(t => t.Action.Code).ThenBy(t => t.Activite == null ? "" : t.Activite.Code)
            .Select(t => new PartTemps(t.ActionId, t.Action.Code + " — " + t.Action.Libelle, t.ActiviteId,
                t.Activite == null ? null : t.Activite.Code + " — " + t.Activite.Libelle, t.PourcentageTemps))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> DefinirTempsAsync(Ulid exerciceId, Ulid agentId, IReadOnlyList<(Ulid ActionId, Ulid? ActiviteId, decimal Pourcentage)> parts)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.ProgrammationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir la répartition du temps"));

        if (parts.Select(p => (p.ActionId, p.ActiviteId)).Distinct().Count() != parts.Count) return (false, "Une même destination figure deux fois.");
        if (parts.Any(p => p.Pourcentage is <= 0 or > 100 || decimal.Round(p.Pourcentage, 4) != p.Pourcentage))
            return (false, "Chaque part est comprise entre 0 (exclu) et 100 %, 4 décimales au plus.");
        var total = parts.Sum(p => p.Pourcentage);
        if (total > 100) return (false, $"Le temps d'un agent ne dépasse pas 100 % (actuellement {total:0.####} %).");

        await using var ctx = await _factory.CreateDbContextAsync();
        var (_, erreurExercice) = await AccesBudgetProgramme.ExerciceSaisissableAsync(ctx, exerciceId, preparationAdmise: true);
        if (erreurExercice is not null) return (false, erreurExercice);

        var agent = await ctx.Agents.AsNoTracking().Where(a => a.Id == agentId).Select(a => new { a.Id, a.StructureId }).FirstOrDefaultAsync();
        if (agent is null) return (false, "Agent introuvable.");

        var actionsIds = parts.Select(p => p.ActionId).Distinct().ToList();
        var actions = await ctx.Actions.AsNoTracking().Where(a => actionsIds.Contains(a.Id) && a.EstActif)
            .Select(a => new { a.Id, a.ProgrammeId }).ToListAsync();
        if (actions.Count != actionsIds.Count) return (false, "Une des actions est introuvable ou inactive.");
        foreach (var p in parts.Where(p => p.ActiviteId is not null))
            if (!await ctx.Activites.AnyAsync(a => a.Id == p.ActiviteId && a.ActionId == p.ActionId))
                return (false, "Une activité ne correspond pas à son action.");

        // Périmètre : la structure de l'agent, ou chacune des actions.
        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(StructureId: agent.StructureId));
        if (refus is not null)
        {
            refus = null;
            foreach (var a in actions) refus ??= await _acces.PerimetreAsync(acteurId, new CiblePerimetre(a.ProgrammeId, a.Id));
            if (refus is not null) return (false, refus);
        }

        var actuelles = await ctx.RepartitionsTemps.Where(t => t.ExerciceId == exerciceId && t.AgentId == agentId).ToListAsync();
        foreach (var t in actuelles.Where(t => !parts.Any(p => p.ActionId == t.ActionId && p.ActiviteId == t.ActiviteId))) Supprimer(t, acteurId);
        foreach (var (actionId, activiteId, pourcentage) in parts)
        {
            var part = actuelles.FirstOrDefault(t => t.ActionId == actionId && t.ActiviteId == activiteId);
            if (part is null)
                ctx.RepartitionsTemps.Add(new RepartitionTemps { ExerciceId = exerciceId, AgentId = agentId, ActionId = actionId, ActiviteId = activiteId, PourcentageTemps = pourcentage, CreePar = acteurId });
            else
            {
                part.PourcentageTemps = pourcentage;
                part.ModifiePar = acteurId;
            }
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "REPARTITION_TEMPS", AccesBudgetProgramme.ModuleAudit, nameof(Agent),
            agentId.ToString(), null, parts.Select(p => new { Action = p.ActionId.ToString(), Activite = p.ActiviteId?.ToString(), p.Pourcentage }));
        return (true, null);
    }

    // ═════ Ventilation des charges ════════════════════════════════════════════

    public async Task<List<LigneVentilation>> VentilerAsync(Ulid exerciceId, decimal chargesPersonnel, decimal chargesCommunes)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        // Poids de chaque action : somme des parts de temps de tous les agents (activités comprises).
        var poids = await ctx.RepartitionsTemps.AsNoTracking()
            .Where(t => t.ExerciceId == exerciceId && t.Agent.EstActif)
            .GroupBy(t => new { t.ActionId, Programme = t.Action.Programme.Code, POrdre = t.Action.Programme.Ordre, t.Action.Code, t.Action.Libelle })
            .Select(g => new { g.Key, Poids = g.Sum(x => x.PourcentageTemps) })
            .ToListAsync();

        var personnel = CalculsBudgetProgramme.Ventiler(chargesPersonnel, poids.Select(p => (p.Key.ActionId, p.Poids))).ToDictionary(x => x.Cle, x => x.Montant);
        var communes = CalculsBudgetProgramme.Ventiler(chargesCommunes, poids.Select(p => (p.Key.ActionId, p.Poids))).ToDictionary(x => x.Cle, x => x.Montant);
        var totalPoids = poids.Sum(p => p.Poids);

        return poids.OrderBy(p => p.Key.POrdre).ThenBy(p => p.Key.Code)
            .Select(p => new LigneVentilation(p.Key.Programme, p.Key.Code, p.Key.Libelle,
                totalPoids == 0 ? 0 : Math.Round(100m * p.Poids / totalPoids, 2),
                personnel.GetValueOrDefault(p.Key.ActionId), communes.GetValueOrDefault(p.Key.ActionId)))
            .ToList();
    }

    private static void Supprimer(BaseEntity e, Ulid acteurId)
    {
        e.EstSupprime = true;
        e.DateSuppression = DateTime.UtcNow;
        e.SupprimePar = acteurId;
    }
}
