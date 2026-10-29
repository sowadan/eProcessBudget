using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

public enum NiveauNomenclature { Axe, Rubrique, SousRubrique, Ligne }

/// <summary>Nœud de l'arborescence axe › rubrique › sous-rubrique › ligne.</summary>
/// <param name="Detail">Résumé affiché à côté du libellé (sens, nature, source pour une ligne).</param>
/// <param name="SansCompte">Ligne sans compte d'imputation (règle § 4.2) : signalée à l'écran.</param>
public sealed record NoeudNomenclature(
    Ulid Id, NiveauNomenclature Niveau, string Code, string Libelle, bool EstActif, int NbEnfants,
    string? Detail = null, bool SansCompte = false);

public sealed record CompteLigne(Ulid CompteId, string Numero, string Libelle, bool EstPrincipal);

/// <summary>
/// Nomenclature budgétaire en arborescence (cahier technique § 4.2) et correspondances
/// (ligne ↔ comptes du plan comptable, site ↔ collectivités).
///
/// Chargement par niveau : on ne lit que les enfants du nœud déplié, jamais tout l'arbre.
/// La création et la modification des nœuds passent par les services de référentiel
/// (IReferentielService&lt;Rubrique&gt;...), qui portent les règles.
/// </summary>
public interface INomenclatureService
{
    Task<List<NoeudNomenclature>> ListerAxesAsync();
    Task<List<NoeudNomenclature>> ListerEnfantsAsync(NiveauNomenclature niveauParent, Ulid parentId);

    Task<List<CompteLigne>> ObtenirComptesLigneAsync(Ulid ligneId);

    /// <summary>Comptes d'imputation d'une ligne : au moins un, dont un principal (règle § 4.2).</summary>
    Task<(bool Ok, string? Erreur)> DefinirComptesLigneAsync(Ulid ligneId, IReadOnlyCollection<Ulid> comptes, Ulid? principal);

    Task<List<ElementListe>> ObtenirCollectivitesSiteAsync(Ulid siteId);

    /// <summary>Collectivités déjà rattachées à un autre poste (C.2.8) : identifiant → nom du poste.</summary>
    Task<Dictionary<Ulid, string>> CollectivitesRattacheesAilleursAsync(Ulid siteId);
    Task<(bool Ok, string? Erreur)> DefinirCollectivitesSiteAsync(Ulid siteId, IReadOnlyCollection<Ulid> tiers);
}

public sealed class NomenclatureService : INomenclatureService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IAuditService _audit;

    public NomenclatureService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<NoeudNomenclature>> ListerAxesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var axes = await ctx.Axes.AsNoTracking()
            .OrderBy(a => a.Ordre).ThenBy(a => a.Code)
            .Select(a => new { a.Id, a.Code, a.Libelle, a.EstActif, a.TypeAxe, a.Section, Nb = a.Rubriques.Count() })
            .ToListAsync();

        return axes.Select(a => new NoeudNomenclature(a.Id, NiveauNomenclature.Axe, a.Code, a.Libelle, a.EstActif, a.Nb,
            $"{LibellesParametrage.De(LibellesParametrage.TypeAxe, a.TypeAxe)} · {LibellesParametrage.De(LibellesParametrage.Section, a.Section)}")).ToList();
    }

    public async Task<List<NoeudNomenclature>> ListerEnfantsAsync(NiveauNomenclature niveauParent, Ulid parentId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return niveauParent switch
        {
            NiveauNomenclature.Axe => await ctx.Rubriques.AsNoTracking()
                .Where(r => r.AxeId == parentId).OrderBy(r => r.Ordre).ThenBy(r => r.Code)
                .Select(r => new NoeudNomenclature(r.Id, NiveauNomenclature.Rubrique, r.Code, r.Libelle, r.EstActif,
                    r.SousRubriques.Count(), r.Note, false))
                .ToListAsync(),

            NiveauNomenclature.Rubrique => await ctx.SousRubriques.AsNoTracking()
                .Where(s => s.RubriqueId == parentId).OrderBy(s => s.Ordre).ThenBy(s => s.Code)
                .Select(s => new NoeudNomenclature(s.Id, NiveauNomenclature.SousRubrique, s.Code, s.Libelle, s.EstActif,
                    s.Lignes.Count(), null, false))
                .ToListAsync(),

            NiveauNomenclature.SousRubrique => (await ctx.LignesBudgetaires.AsNoTracking()
                .Where(l => l.SousRubriqueId == parentId).OrderBy(l => l.Code)
                .Select(l => new
                {
                    l.Id, l.Code, l.Libelle, l.EstActif, l.Sens,
                    Nature = l.NatureDepense != null ? l.NatureDepense.Libelle : l.NatureRecette != null ? l.NatureRecette.Libelle : null,
                    Source = l.SourceFinancement.Code,
                    SansCompte = !l.Comptes.Any()
                })
                .ToListAsync())
                .Select(l => new NoeudNomenclature(l.Id, NiveauNomenclature.Ligne, l.Code, l.Libelle, l.EstActif, 0,
                    $"{LibellesParametrage.De(LibellesParametrage.SensLigne, l.Sens)} · {l.Nature} · {l.Source}", l.SansCompte))
                .ToList(),

            _ => []
        };
    }

    // ═════ Ligne ↔ comptes ════════════════════════════════════════════════════

    public async Task<List<CompteLigne>> ObtenirComptesLigneAsync(Ulid ligneId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.LignesComptes.AsNoTracking()
            .Where(l => l.LigneBudgetaireId == ligneId)
            .OrderByDescending(l => l.EstPrincipal).ThenBy(l => l.CompteComptable.Numero)
            .Select(l => new CompteLigne(l.CompteComptableId, l.CompteComptable.Numero, l.CompteComptable.Libelle, l.EstPrincipal))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> DefinirComptesLigneAsync(Ulid ligneId, IReadOnlyCollection<Ulid> comptes, Ulid? principal)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        var cibles = comptes.Distinct().ToList();
        if (cibles.Count == 0) return (false, "Une ligne budgétaire est imputée sur au moins un compte.");
        principal ??= cibles[0];
        if (!cibles.Contains(principal.Value)) return (false, "Le compte principal doit faire partie des comptes de la ligne.");

        await using var ctx = await _factory.CreateDbContextAsync();
        if (!await ctx.LignesBudgetaires.AnyAsync(l => l.Id == ligneId)) return (false, "Ligne budgétaire introuvable.");

        var actuels = await ctx.LignesComptes.Where(l => l.LigneBudgetaireId == ligneId).ToListAsync();
        var dejaLies = actuels.Select(l => l.CompteComptableId).ToHashSet();

        // Un nouveau compte doit exister dans l'organisme et être actif.
        var nouveaux = cibles.Where(c => !dejaLies.Contains(c)).ToList();
        var valides = await ctx.ComptesComptables.Where(c => nouveaux.Contains(c.Id) && c.EstActif).CountAsync();
        if (valides != nouveaux.Count) return (false, "Un des comptes choisis est introuvable ou inactif.");

        ctx.LignesComptes.RemoveRange(actuels.Where(l => !cibles.Contains(l.CompteComptableId)));
        foreach (var lien in actuels.Where(l => cibles.Contains(l.CompteComptableId)))
            lien.EstPrincipal = lien.CompteComptableId == principal;
        foreach (var compteId in nouveaux)
            ctx.LignesComptes.Add(new LigneCompte
            {
                LigneBudgetaireId = ligneId, CompteComptableId = compteId, EstPrincipal = compteId == principal, CreePar = acteurId
            });

        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CORRESPONDANCE_COMPTES", AccesParametrage.ModuleAudit, nameof(LigneBudgetaire),
            ligneId.ToString(), dejaLies.Select(i => i.ToString()), new { Comptes = cibles.Select(i => i.ToString()), Principal = principal.ToString() });
        return (true, null);
    }

    // ═════ Site ↔ collectivités ═══════════════════════════════════════════════

    public async Task<List<ElementListe>> ObtenirCollectivitesSiteAsync(Ulid siteId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.SitesTiers.AsNoTracking()
            .Where(s => s.SiteId == siteId)
            .OrderBy(s => s.Tiers.RaisonSociale)
            .Select(s => new ElementListe(s.TiersId, s.Tiers.Code + " — " + s.Tiers.RaisonSociale))
            .ToListAsync();
    }

    public async Task<Dictionary<Ulid, string>> CollectivitesRattacheesAilleursAsync(Ulid siteId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.SitesTiers.AsNoTracking()
            .Where(s => s.SiteId != siteId)
            .Select(s => new { s.TiersId, s.Site.Nom })
            .ToDictionaryAsync(s => s.TiersId, s => s.Nom);
    }

    public async Task<(bool Ok, string? Erreur)> DefinirCollectivitesSiteAsync(Ulid siteId, IReadOnlyCollection<Ulid> tiers)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        var cibles = tiers.Distinct().ToList();
        await using var ctx = await _factory.CreateDbContextAsync();
        if (!await ctx.Sites.AnyAsync(s => s.Id == siteId)) return (false, "Site introuvable.");

        var actuels = await ctx.SitesTiers.Where(s => s.SiteId == siteId).ToListAsync();
        var dejaLies = actuels.Select(s => s.TiersId).ToHashSet();
        var nouveaux = cibles.Where(t => !dejaLies.Contains(t)).ToList();
        if (await ctx.Tiers.CountAsync(t => nouveaux.Contains(t.Id) && t.EstActif) != nouveaux.Count)
            return (false, "Une des collectivités choisies est introuvable ou inactive.");

        // Réponse C.2.8 : une collectivité n'est rattachée qu'à un seul poste.
        var ailleurs = await ctx.SitesTiers.AsNoTracking()
            .Where(s => nouveaux.Contains(s.TiersId) && s.SiteId != siteId)
            .Select(s => s.Tiers.RaisonSociale + " (" + s.Site.Nom + ")")
            .ToListAsync();
        if (ailleurs.Count > 0)
            return (false, $"Une collectivité n'est rattachée qu'à un seul poste. Déjà rattachée : {string.Join(", ", ailleurs)}.");

        ctx.SitesTiers.RemoveRange(actuels.Where(s => !cibles.Contains(s.TiersId)));
        foreach (var tiersId in nouveaux)
            ctx.SitesTiers.Add(new SiteTiers { SiteId = siteId, TiersId = tiersId, CreePar = acteurId });
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CORRESPONDANCE_COLLECTIVITES", AccesParametrage.ModuleAudit, nameof(Site),
            siteId.ToString(), dejaLies.Select(i => i.ToString()), cibles.Select(i => i.ToString()));
        return (true, null);
    }
}
