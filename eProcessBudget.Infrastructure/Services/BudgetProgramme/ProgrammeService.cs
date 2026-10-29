using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

public enum NiveauProgramme { Programme, Action, Activite }

/// <summary>Nœud de l'arborescence programme › action › activité.</summary>
public sealed record NoeudProgramme(
    Ulid Id, NiveauProgramme Niveau, string Code, string Libelle, bool EstActif, int NbEnfants, int NbIndicateurs,
    string? Detail = null);

public sealed record StructureProgramme(Ulid StructureId, string Libelle, bool EstPrincipale);

/// <summary>
/// Programmation stratégique (cahier fonctionnel § 3.3.2) : arborescence des programmes,
/// actions et activités, structures responsables conjointes, responsable unique.
/// La création et la modification des éléments passent par IReferentielService&lt;Programme&gt;
/// (et Action, Activité), qui portent les règles et l'import Excel.
/// </summary>
public interface IProgrammeService
{
    Task<List<NoeudProgramme>> ListerProgrammesAsync();
    Task<List<NoeudProgramme>> ListerEnfantsAsync(NiveauProgramme niveauParent, Ulid parentId);

    /// <summary>Comptes actifs de l'organisme : choix du responsable d'un programme.</summary>
    Task<List<ElementListe>> ListerResponsablesPossiblesAsync();

    Task<List<StructureProgramme>> ObtenirStructuresAsync(Ulid programmeId);

    /// <summary>Structures responsables conjointes : au moins une, dont une principale.</summary>
    Task<(bool Ok, string? Erreur)> DefinirStructuresAsync(Ulid programmeId, IReadOnlyCollection<Ulid> structures, Ulid? principale);
}

public sealed class ProgrammeService : IProgrammeService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesBudgetProgramme _acces;
    private readonly IAuditService _audit;

    public ProgrammeService(IDbContextFactory<AppDbContext> factory, AccesBudgetProgramme acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<NoeudProgramme>> ListerProgrammesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var programmes = await ctx.Programmes.AsNoTracking()
            .OrderBy(p => p.Ordre).ThenBy(p => p.Code)
            .Select(p => new
            {
                p.Id, p.Code, p.Libelle, p.EstActif, p.ResponsableId,
                NbActions = p.Actions.Count(),
                NbIndicateurs = ctx.Indicateurs.Count(i => i.ProgrammeId == p.Id)
            })
            .ToListAsync();

        var ids = programmes.Select(p => p.ResponsableId).Distinct().ToList();
        var noms = await ctx.Utilisateurs.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.NomPrenoms);

        return programmes.Select(p => new NoeudProgramme(p.Id, NiveauProgramme.Programme, p.Code, p.Libelle, p.EstActif,
            p.NbActions, p.NbIndicateurs, noms.TryGetValue(p.ResponsableId, out var n) ? $"Responsable : {n}" : null)).ToList();
    }

    public async Task<List<NoeudProgramme>> ListerEnfantsAsync(NiveauProgramme niveauParent, Ulid parentId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return niveauParent switch
        {
            NiveauProgramme.Programme => await ctx.Actions.AsNoTracking()
                .Where(a => a.ProgrammeId == parentId).OrderBy(a => a.Ordre).ThenBy(a => a.Code)
                .Select(a => new NoeudProgramme(a.Id, NiveauProgramme.Action, a.Code, a.Libelle, a.EstActif,
                    a.Activites.Count(), ctx.Indicateurs.Count(i => i.ActionId == a.Id),
                    a.Structure == null ? null : a.Structure.Code))
                .ToListAsync(),
            NiveauProgramme.Action => await ctx.Activites.AsNoTracking()
                .Where(a => a.ActionId == parentId).OrderBy(a => a.Code)
                .Select(a => new NoeudProgramme(a.Id, NiveauProgramme.Activite, a.Code, a.Libelle, a.EstActif, 0, 0,
                    a.NatureDepensePrincipale == null ? null : a.NatureDepensePrincipale.Libelle))
                .ToListAsync(),
            _ => []
        };
    }

    public async Task<List<ElementListe>> ListerResponsablesPossiblesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var organismeId = ctx.OrganismeCourantId;
        return await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.OrganismeId == organismeId && u.EstActif)
            .OrderBy(u => u.NomPrenoms)
            .Select(u => new ElementListe(u.Id, u.NomPrenoms + " (" + u.Matricule + ")"))
            .ToListAsync();
    }

    public async Task<List<StructureProgramme>> ObtenirStructuresAsync(Ulid programmeId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ProgrammeStructures.AsNoTracking()
            .Where(s => s.ProgrammeId == programmeId)
            .OrderByDescending(s => s.EstPrincipale).ThenBy(s => s.Structure.Code)
            .Select(s => new StructureProgramme(s.StructureId, s.Structure.Code + " — " + s.Structure.Libelle, s.EstPrincipale))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> DefinirStructuresAsync(Ulid programmeId, IReadOnlyCollection<Ulid> structures, Ulid? principale)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.BudgetProgrammeDefinir);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("définir les programmes"));

        var cibles = structures.Distinct().ToList();
        if (cibles.Count == 0) return (false, "Un programme a au moins une structure responsable.");
        principale ??= cibles[0];
        if (!cibles.Contains(principale.Value)) return (false, "La structure principale doit faire partie des structures du programme.");

        await using var ctx = await _factory.CreateDbContextAsync();
        if (!await ctx.Programmes.AnyAsync(p => p.Id == programmeId)) return (false, "Programme introuvable.");

        var actuels = await ctx.ProgrammeStructures.Where(s => s.ProgrammeId == programmeId).ToListAsync();
        var dejaLies = actuels.Select(s => s.StructureId).ToHashSet();
        var nouvelles = cibles.Where(c => !dejaLies.Contains(c)).ToList();
        if (await ctx.Structures.CountAsync(s => nouvelles.Contains(s.Id) && s.EstActif) != nouvelles.Count)
            return (false, "Une des structures choisies est introuvable ou inactive.");

        ctx.ProgrammeStructures.RemoveRange(actuels.Where(s => !cibles.Contains(s.StructureId)));
        foreach (var lien in actuels.Where(s => cibles.Contains(s.StructureId)))
            lien.EstPrincipale = lien.StructureId == principale;
        foreach (var structureId in nouvelles)
            ctx.ProgrammeStructures.Add(new ProgrammeStructure
            {
                ProgrammeId = programmeId, StructureId = structureId, EstPrincipale = structureId == principale, CreePar = acteurId
            });
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "STRUCTURES_PROGRAMME", AccesBudgetProgramme.ModuleAudit, nameof(Programme),
            programmeId.ToString(), dejaLies.Select(i => i.ToString()), new { Structures = cibles.Select(i => i.ToString()), Principale = principale.ToString() });
        return (true, null);
    }
}
