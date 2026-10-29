using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

/// <summary>AE et CP d'une année.</summary>
public readonly record struct MontantsAECP(decimal AE, decimal CP);

/// <summary>Ligne de programmation financière : une action (et activité), une nature, une source, quatre années.</summary>
public sealed record LigneProgrammationFinanciere(
    Ulid ActionId, string Action, Ulid? ActiviteId, string? Activite,
    Ulid NatureDepenseId, string Nature, Ulid SourceFinancementId, string Source,
    IReadOnlyDictionary<int, MontantsAECP> Montants);

/// <summary>Saisie d'une ligne de programmation financière.</summary>
public sealed class SaisieProgrammationFinanciere
{
    public Ulid CycleId { get; set; }
    public Ulid ActionId { get; set; }
    public Ulid? ActiviteId { get; set; }
    public Ulid NatureDepenseId { get; set; }
    public Ulid SourceFinancementId { get; set; }
    public Dictionary<int, MontantsAECP> Montants { get; set; } = new();
}

public sealed record LigneRessource(Ulid NatureRecetteId, string Nature, Ulid SourceFinancementId, string Source, IReadOnlyDictionary<int, decimal> Montants);
public sealed record LigneEmploi(Ulid ProgrammeId, string Programme, Ulid CategorieEmploiId, string Categorie, IReadOnlyDictionary<int, int> Effectifs);

/// <summary>Coût d'un programme, d'une action ou d'une nature, par année (§ 3.3.5).</summary>
public sealed record LigneRecapitulatif(string Programme, string? Action, string? Nature, GroupeNature? Groupe, string? Source,
                                        IReadOnlyDictionary<int, MontantsAECP> Montants);

/// <summary>Équilibre ressources / emplois d'une année.</summary>
public sealed record EquilibreAnnee(int Annee, decimal Ressources, decimal AE, decimal CP)
{
    public decimal Solde => Ressources - CP;
}

/// <summary>Effectifs et dépenses de personnel d'un programme, par année (§ 3.3.6).</summary>
public sealed record EffectifsEtPersonnel(string Programme, IReadOnlyDictionary<int, (int Effectif, decimal CPPersonnel)> Annees);

/// <summary>
/// Programmation pluriannuelle d'un cycle (cahier fonctionnel § 3.3.5 et 3.3.6) : AE et CP
/// par action, activité, nature de dépense et source, pour N-1 et les trois années
/// programmées ; ressources par nature de recette ; effectifs par catégorie d'emplois ;
/// récapitulatifs et équilibre. Saisie réservée au Responsable Budget (ProgrammationSaisir),
/// dans un cycle en brouillon et dans son périmètre d'habilitation.
/// </summary>
public interface IProgrammationService
{
    Task<List<LigneProgrammationFinanciere>> ListerFinancierAsync(Ulid cycleId, Ulid programmeId);
    Task<(bool Ok, string? Erreur)> EnregistrerFinancierAsync(SaisieProgrammationFinanciere saisie);
    Task<(bool Ok, string? Erreur)> SupprimerFinancierAsync(Ulid cycleId, Ulid actionId, Ulid? activiteId, Ulid natureId, Ulid sourceId);

    Task<List<LigneRessource>> ListerRessourcesAsync(Ulid cycleId);
    Task<(bool Ok, string? Erreur)> EnregistrerRessourceAsync(Ulid cycleId, Ulid natureRecetteId, Ulid sourceId, IReadOnlyDictionary<int, decimal> montants);

    Task<List<LigneEmploi>> ListerEmploisAsync(Ulid cycleId);
    Task<(bool Ok, string? Erreur)> EnregistrerEmploiAsync(Ulid cycleId, Ulid programmeId, Ulid categorieId, IReadOnlyDictionary<int, int> effectifs);

    /// <summary>Coût par programme, puis par action, puis par nature de dépense et source.</summary>
    Task<List<LigneRecapitulatif>> RecapitulatifAsync(Ulid cycleId);
    Task<List<EquilibreAnnee>> EquilibreAsync(Ulid cycleId);
    Task<List<EffectifsEtPersonnel>> EffectifsEtPersonnelAsync(Ulid cycleId);
}

public sealed class ProgrammationService : IProgrammationService
{
    /// <summary>Code de la nature « Personnel » dans les données initiales (§ 3.7).</summary>
    public const string CodeNaturePersonnel = "1";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesBudgetProgramme _acces;
    private readonly IAuditService _audit;

    public ProgrammationService(IDbContextFactory<AppDbContext> factory, AccesBudgetProgramme acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    // ═════ AE / CP ════════════════════════════════════════════════════════════

    public async Task<List<LigneProgrammationFinanciere>> ListerFinancierAsync(Ulid cycleId, Ulid programmeId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var lignes = await ctx.ProgrammationsFinancieres.AsNoTracking()
            .Where(p => p.CycleProgrammationId == cycleId && p.Action.ProgrammeId == programmeId)
            .Select(p => new
            {
                p.ActionId, Action = p.Action.Code, p.ActiviteId, Activite = p.Activite == null ? null : p.Activite.Code,
                p.NatureDepenseId, Nature = p.NatureDepense.Code + " " + p.NatureDepense.Libelle,
                p.SourceFinancementId, Source = p.SourceFinancement.Code, p.Annee, p.MontantAE, p.MontantCP
            })
            .ToListAsync();

        return lignes
            .GroupBy(l => (l.ActionId, l.Action, l.ActiviteId, l.Activite, l.NatureDepenseId, l.Nature, l.SourceFinancementId, l.Source))
            .OrderBy(g => g.Key.Action).ThenBy(g => g.Key.Activite).ThenBy(g => g.Key.Nature).ThenBy(g => g.Key.Source)
            .Select(g => new LigneProgrammationFinanciere(g.Key.ActionId, g.Key.Action, g.Key.ActiviteId, g.Key.Activite,
                g.Key.NatureDepenseId, g.Key.Nature, g.Key.SourceFinancementId, g.Key.Source,
                g.GroupBy(x => x.Annee).ToDictionary(a => a.Key, a => new MontantsAECP(a.Sum(x => x.MontantAE), a.Sum(x => x.MontantCP)))))
            .ToList();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerFinancierAsync(SaisieProgrammationFinanciere s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.ProgrammationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir la programmation"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var (cycle, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, s.CycleId);
        if (erreur is not null) return (false, erreur);
        var annees = AccesBudgetProgramme.Annees(cycle!);

        var action = await ctx.Actions.AsNoTracking().Where(a => a.Id == s.ActionId && a.EstActif)
                              .Select(a => new { a.Id, a.ProgrammeId }).FirstOrDefaultAsync();
        if (action is null) return (false, "Action introuvable ou inactive.");
        if (s.ActiviteId is { } activiteId && !await ctx.Activites.AnyAsync(a => a.Id == activiteId && a.ActionId == s.ActionId))
            return (false, "L'activité doit appartenir à l'action.");
        if (!await ctx.NaturesDepense.AnyAsync(n => n.Id == s.NatureDepenseId)) return (false, "Nature de dépense introuvable.");
        if (!await ctx.SourcesFinancement.AnyAsync(n => n.Id == s.SourceFinancementId)) return (false, "Source de financement introuvable.");

        var montantErreur = ControlerAECP(s.Montants, annees);
        if (montantErreur is not null) return (false, montantErreur);

        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(action.ProgrammeId, action.Id));
        if (refus is not null) return (false, refus);

        var existantes = await ctx.ProgrammationsFinancieres
            .Where(p => p.CycleProgrammationId == s.CycleId && p.ActionId == s.ActionId && p.ActiviteId == s.ActiviteId
                     && p.NatureDepenseId == s.NatureDepenseId && p.SourceFinancementId == s.SourceFinancementId)
            .ToListAsync();

        foreach (var annee in annees)
        {
            var m = s.Montants.GetValueOrDefault(annee);
            var ligne = existantes.FirstOrDefault(p => p.Annee == annee);
            if (m.AE == 0 && m.CP == 0)
            {
                if (ligne is not null) Supprimer(ligne, acteurId);
                continue;
            }
            if (ligne is null)
            {
                ligne = new ProgrammationFinanciere
                {
                    CycleProgrammationId = s.CycleId, Annee = annee, ActionId = s.ActionId, ActiviteId = s.ActiviteId,
                    NatureDepenseId = s.NatureDepenseId, SourceFinancementId = s.SourceFinancementId, CreePar = acteurId
                };
                ctx.ProgrammationsFinancieres.Add(ligne);
            }
            ligne.MontantAE = m.AE;
            ligne.MontantCP = m.CP;
            ligne.ModifiePar = acteurId;
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "PROGRAMMATION_AECP", AccesBudgetProgramme.ModuleAudit, nameof(ProgrammationFinanciere),
            s.ActionId.ToString(), null, new { Cycle = s.CycleId.ToString(), Montants = s.Montants.ToDictionary(k => k.Key, k => new { k.Value.AE, k.Value.CP }) });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerFinancierAsync(Ulid cycleId, Ulid actionId, Ulid? activiteId, Ulid natureId, Ulid sourceId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.ProgrammationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir la programmation"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var (_, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, cycleId);
        if (erreur is not null) return (false, erreur);
        var programmeId = await ctx.Actions.Where(a => a.Id == actionId).Select(a => (Ulid?)a.ProgrammeId).FirstOrDefaultAsync();
        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(programmeId, actionId));
        if (refus is not null) return (false, refus);

        var lignes = await ctx.ProgrammationsFinancieres
            .Where(p => p.CycleProgrammationId == cycleId && p.ActionId == actionId && p.ActiviteId == activiteId
                     && p.NatureDepenseId == natureId && p.SourceFinancementId == sourceId)
            .ToListAsync();
        foreach (var l in lignes) Supprimer(l, acteurId);
        await ctx.SaveChangesAsync();
        return (true, null);
    }

    /// <summary>
    /// Montants positifs à 2 décimales ; les CP cumulés ne dépassent jamais les AE cumulées
    /// (règle § 4.2 : « les CP d'un exercice ne peuvent excéder les AE disponibles cumulées »).
    /// </summary>
    internal static string? ControlerAECP(IReadOnlyDictionary<int, MontantsAECP> montants, int[] annees)
    {
        if (montants.Keys.Any(a => !annees.Contains(a))) return $"Les montants portent sur les années {annees[0]} à {annees[^1]}.";
        if (montants.Values.Any(m => m.AE < 0 || m.CP < 0)) return "Les montants sont positifs.";
        if (montants.Values.Any(m => decimal.Round(m.AE, 2) != m.AE || decimal.Round(m.CP, 2) != m.CP)) return "Les montants comptent 2 décimales au plus.";

        decimal ae = 0, cp = 0;
        foreach (var annee in annees)
        {
            var m = montants.GetValueOrDefault(annee);
            ae += m.AE;
            cp += m.CP;
            if (cp > ae) return $"{annee} : les CP cumulés ({cp:N2}) dépassent les AE cumulées ({ae:N2}).";
        }
        return null;
    }

    // ═════ Ressources ═════════════════════════════════════════════════════════

    public async Task<List<LigneRessource>> ListerRessourcesAsync(Ulid cycleId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var lignes = await ctx.ProgrammationsRessources.AsNoTracking()
            .Where(r => r.CycleProgrammationId == cycleId)
            .Select(r => new { r.NatureRecetteId, Nature = r.NatureRecette.Code + " " + r.NatureRecette.Libelle, r.SourceFinancementId, Source = r.SourceFinancement.Code, r.Annee, r.Montant })
            .ToListAsync();
        return lignes.GroupBy(l => (l.NatureRecetteId, l.Nature, l.SourceFinancementId, l.Source))
                     .OrderBy(g => g.Key.Nature).ThenBy(g => g.Key.Source)
                     .Select(g => new LigneRessource(g.Key.NatureRecetteId, g.Key.Nature, g.Key.SourceFinancementId, g.Key.Source,
                                                     g.ToDictionary(x => x.Annee, x => x.Montant)))
                     .ToList();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerRessourceAsync(Ulid cycleId, Ulid natureRecetteId, Ulid sourceId, IReadOnlyDictionary<int, decimal> montants)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.ProgrammationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir la programmation"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var (cycle, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, cycleId);
        if (erreur is not null) return (false, erreur);
        var annees = AccesBudgetProgramme.Annees(cycle!);
        if (montants.Keys.Any(a => !annees.Contains(a)) || montants.Values.Any(m => m < 0 || decimal.Round(m, 2) != m))
            return (false, "Montants positifs à 2 décimales, sur les années du cycle.");
        if (!await ctx.NaturesRecette.AnyAsync(n => n.Id == natureRecetteId)) return (false, "Nature de recette introuvable.");
        if (!await ctx.SourcesFinancement.AnyAsync(n => n.Id == sourceId)) return (false, "Source de financement introuvable.");

        // Les ressources ne relèvent d'aucun programme : périmètre de l'organisme entier.
        var refus = await _acces.PerimetreAsync(acteurId, CiblePerimetre.Organisme);
        if (refus is not null) return (false, refus);

        var existantes = await ctx.ProgrammationsRessources
            .Where(r => r.CycleProgrammationId == cycleId && r.NatureRecetteId == natureRecetteId && r.SourceFinancementId == sourceId)
            .ToListAsync();
        foreach (var annee in annees)
        {
            var montant = montants.GetValueOrDefault(annee);
            var ligne = existantes.FirstOrDefault(r => r.Annee == annee);
            if (montant == 0) { if (ligne is not null) Supprimer(ligne, acteurId); continue; }
            if (ligne is null)
            {
                ligne = new ProgrammationRessource { CycleProgrammationId = cycleId, Annee = annee, NatureRecetteId = natureRecetteId, SourceFinancementId = sourceId, CreePar = acteurId };
                ctx.ProgrammationsRessources.Add(ligne);
            }
            ligne.Montant = montant;
            ligne.ModifiePar = acteurId;
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "PROGRAMMATION_RESSOURCES", AccesBudgetProgramme.ModuleAudit, nameof(ProgrammationRessource),
            natureRecetteId.ToString(), null, new { Cycle = cycleId.ToString(), Montants = montants });
        return (true, null);
    }

    // ═════ Emplois ════════════════════════════════════════════════════════════

    public async Task<List<LigneEmploi>> ListerEmploisAsync(Ulid cycleId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var lignes = await ctx.ProgrammationsEmplois.AsNoTracking()
            .Where(e => e.CycleProgrammationId == cycleId)
            .Select(e => new { e.ProgrammeId, Programme = e.Programme.Code, e.CategorieEmploiId, Categorie = e.CategorieEmploi.Code, Ordre = e.CategorieEmploi.Ordre, e.Annee, e.Effectif })
            .ToListAsync();
        return lignes.GroupBy(l => (l.ProgrammeId, l.Programme, l.CategorieEmploiId, l.Categorie, l.Ordre))
                     .OrderBy(g => g.Key.Programme).ThenBy(g => g.Key.Ordre)
                     .Select(g => new LigneEmploi(g.Key.ProgrammeId, g.Key.Programme, g.Key.CategorieEmploiId, g.Key.Categorie,
                                                  g.ToDictionary(x => x.Annee, x => x.Effectif)))
                     .ToList();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerEmploiAsync(Ulid cycleId, Ulid programmeId, Ulid categorieId, IReadOnlyDictionary<int, int> effectifs)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.ProgrammationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir la programmation"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var (cycle, erreur) = await AccesBudgetProgramme.CycleModifiableAsync(ctx, cycleId);
        if (erreur is not null) return (false, erreur);
        var annees = AccesBudgetProgramme.Annees(cycle!);
        if (effectifs.Keys.Any(a => !annees.Contains(a)) || effectifs.Values.Any(v => v < 0)) return (false, "Effectifs positifs, sur les années du cycle.");
        if (!await ctx.Programmes.AnyAsync(p => p.Id == programmeId)) return (false, "Programme introuvable.");
        if (!await ctx.CategoriesEmploi.AnyAsync(c => c.Id == categorieId)) return (false, "Catégorie d'emplois introuvable.");

        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(programmeId));
        if (refus is not null) return (false, refus);

        var existantes = await ctx.ProgrammationsEmplois
            .Where(e => e.CycleProgrammationId == cycleId && e.ProgrammeId == programmeId && e.CategorieEmploiId == categorieId).ToListAsync();
        foreach (var annee in annees)
        {
            var effectif = effectifs.GetValueOrDefault(annee);
            var ligne = existantes.FirstOrDefault(e => e.Annee == annee);
            if (effectif == 0) { if (ligne is not null) Supprimer(ligne, acteurId); continue; }
            if (ligne is null)
            {
                ligne = new ProgrammationEmploi { CycleProgrammationId = cycleId, Annee = annee, ProgrammeId = programmeId, CategorieEmploiId = categorieId, CreePar = acteurId };
                ctx.ProgrammationsEmplois.Add(ligne);
            }
            ligne.Effectif = effectif;
            ligne.ModifiePar = acteurId;
        }
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "PROGRAMMATION_EMPLOIS", AccesBudgetProgramme.ModuleAudit, nameof(ProgrammationEmploi),
            programmeId.ToString(), null, new { Cycle = cycleId.ToString(), Effectifs = effectifs });
        return (true, null);
    }

    // ═════ Récapitulatifs ═════════════════════════════════════════════════════

    public async Task<List<LigneRecapitulatif>> RecapitulatifAsync(Ulid cycleId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        // Agrégation côté base : une ligne par (programme, action, nature, source, année).
        var sommes = await ctx.ProgrammationsFinancieres.AsNoTracking()
            .Where(p => p.CycleProgrammationId == cycleId)
            .GroupBy(p => new
            {
                Programme = p.Action.Programme.Code, POrdre = p.Action.Programme.Ordre, Action = p.Action.Code,
                Nature = p.NatureDepense.Code + " " + p.NatureDepense.Libelle, p.NatureDepense.GroupeNature,
                Source = p.SourceFinancement.Code, p.Annee
            })
            .Select(g => new { g.Key, AE = g.Sum(x => x.MontantAE), CP = g.Sum(x => x.MontantCP) })
            .ToListAsync();

        static Dictionary<int, MontantsAECP> ParAnnee(IEnumerable<(int Annee, decimal AE, decimal CP)> l)
            => l.GroupBy(x => x.Annee).ToDictionary(g => g.Key, g => new MontantsAECP(g.Sum(x => x.AE), g.Sum(x => x.CP)));

        var resultat = new List<LigneRecapitulatif>();
        foreach (var prog in sommes.GroupBy(s => (s.Key.Programme, s.Key.POrdre)).OrderBy(g => g.Key.POrdre).ThenBy(g => g.Key.Programme))
        {
            resultat.Add(new(prog.Key.Programme, null, null, null, null, ParAnnee(prog.Select(s => (s.Key.Annee, s.AE, s.CP)))));
            foreach (var action in prog.GroupBy(s => s.Key.Action).OrderBy(g => g.Key))
                resultat.Add(new(prog.Key.Programme, action.Key, null, null, null, ParAnnee(action.Select(s => (s.Key.Annee, s.AE, s.CP)))));
            foreach (var nature in prog.GroupBy(s => (s.Key.Nature, s.Key.GroupeNature, s.Key.Source)).OrderBy(g => g.Key.Nature).ThenBy(g => g.Key.Source))
                resultat.Add(new(prog.Key.Programme, null, nature.Key.Nature, nature.Key.GroupeNature, nature.Key.Source,
                                 ParAnnee(nature.Select(s => (s.Key.Annee, s.AE, s.CP)))));
        }
        return resultat;
    }

    public async Task<List<EquilibreAnnee>> EquilibreAsync(Ulid cycleId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var cycle = await ctx.CyclesProgrammation.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId);
        if (cycle is null) return [];

        var depenses = await ctx.ProgrammationsFinancieres.AsNoTracking().Where(p => p.CycleProgrammationId == cycleId)
            .GroupBy(p => p.Annee).Select(g => new { Annee = g.Key, AE = g.Sum(x => x.MontantAE), CP = g.Sum(x => x.MontantCP) })
            .ToDictionaryAsync(x => x.Annee);
        var ressources = await ctx.ProgrammationsRessources.AsNoTracking().Where(r => r.CycleProgrammationId == cycleId)
            .GroupBy(r => r.Annee).Select(g => new { Annee = g.Key, Total = g.Sum(x => x.Montant) })
            .ToDictionaryAsync(x => x.Annee, x => x.Total);

        return AccesBudgetProgramme.Annees(cycle).Select(a => new EquilibreAnnee(a, ressources.GetValueOrDefault(a),
            depenses.TryGetValue(a, out var d) ? d.AE : 0, depenses.TryGetValue(a, out var d2) ? d2.CP : 0)).ToList();
    }

    public async Task<List<EffectifsEtPersonnel>> EffectifsEtPersonnelAsync(Ulid cycleId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var effectifs = await ctx.ProgrammationsEmplois.AsNoTracking().Where(e => e.CycleProgrammationId == cycleId)
            .GroupBy(e => new { e.Programme.Code, e.Annee }).Select(g => new { g.Key.Code, g.Key.Annee, Total = g.Sum(x => x.Effectif) })
            .ToListAsync();
        var personnel = await ctx.ProgrammationsFinancieres.AsNoTracking()
            .Where(p => p.CycleProgrammationId == cycleId && p.NatureDepense.Code == CodeNaturePersonnel)
            .GroupBy(p => new { p.Action.Programme.Code, p.Annee }).Select(g => new { g.Key.Code, g.Key.Annee, CP = g.Sum(x => x.MontantCP) })
            .ToListAsync();

        return effectifs.Select(e => e.Code).Concat(personnel.Select(p => p.Code)).Distinct().OrderBy(c => c)
            .Select(code => new EffectifsEtPersonnel(code,
                effectifs.Where(e => e.Code == code).Select(e => e.Annee).Concat(personnel.Where(p => p.Code == code).Select(p => p.Annee)).Distinct()
                    .ToDictionary(a => a, a => (effectifs.Where(e => e.Code == code && e.Annee == a).Sum(e => e.Total),
                                                personnel.Where(p => p.Code == code && p.Annee == a).Sum(p => p.CP)))))
            .ToList();
    }

    private static void Supprimer(BaseEntity e, Ulid acteurId)
    {
        e.EstSupprime = true;
        e.DateSuppression = DateTime.UtcNow;
        e.SupprimePar = acteurId;
    }
}
