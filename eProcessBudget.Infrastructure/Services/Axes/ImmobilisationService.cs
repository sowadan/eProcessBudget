using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>Critères de l'inventaire.</summary>
public sealed class FiltreImmobilisations
{
    public Ulid? SiteId { get; set; }
    public Ulid? StructureId { get; set; }
    public Ulid? ProjetId { get; set; }
    public EtatImmobilisation? Etat { get; set; }

    /// <summary>Code inventaire ou désignation.</summary>
    public string? Recherche { get; set; }
}

public sealed record LigneImmobilisation(
    Ulid Id, string Code, string Designation, string Site, string? Structure, string? Projet, string Compte,
    DateOnly DateAcquisition, decimal ValeurAcquisition, EtatImmobilisation Etat);

/// <summary>Inventaire d'un site : nombre de biens et valeur d'acquisition, hors biens cédés ou réformés.</summary>
public sealed record InventaireSite(Ulid SiteId, string Site, TypeSite TypeSite, int NbBiens, decimal Valeur, int NbHorsService);

public sealed class SaisieImmobilisation
{
    public Ulid Id { get; set; }
    public string Code { get; set; } = "";
    public string Designation { get; set; } = "";
    public Ulid? ProjetId { get; set; }
    public Ulid? SiteId { get; set; }
    public Ulid? StructureId { get; set; }
    public Ulid? CompteComptableId { get; set; }
    public DateOnly? DateAcquisition { get; set; }
    public decimal ValeurAcquisition { get; set; }
    public EtatImmobilisation Etat { get; set; } = EtatImmobilisation.Bon;
}

/// <summary>
/// Inventaire des immobilisations et équipements par site (cahier fonctionnel § 3.6.2,
/// technique § 4.5) : chaque bien est rattaché à un site, à un compte d'immobilisation du
/// plan comptable et, le cas échéant, au projet d'investissement qui l'a acquis.
/// </summary>
public interface IImmobilisationService
{
    Task<(List<LigneImmobilisation> Items, int Total, decimal Valeur)> ListerAsync(FiltreImmobilisations filtre, int page = 1, int taille = 20);

    /// <summary>Inventaire résumé de chaque site.</summary>
    Task<List<InventaireSite>> ParSiteAsync();

    Task<SaisieImmobilisation?> ObtenirSaisieAsync(Ulid immobilisationId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieImmobilisation saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid immobilisationId);

    /// <summary>Comptes d'immobilisation actifs, pour un choix.</summary>
    Task<List<ElementListe>> ListerComptesAsync();
}

public sealed class ImmobilisationService : IImmobilisationService
{
    private static readonly AxeDepense Axe = AxesDepense.Investissement;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesAxes _acces;
    private readonly IAuditService _audit;

    public ImmobilisationService(IDbContextFactory<AppDbContext> factory, AccesAxes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<(List<LigneImmobilisation> Items, int Total, decimal Valeur)> ListerAsync(FiltreImmobilisations f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync(Axe) == Ulid.Empty) return ([], 0, 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Immobilisations.AsNoTracking();
        if (f.SiteId is { } site) q = q.Where(i => i.SiteId == site);
        if (f.StructureId is { } structure) q = q.Where(i => i.StructureId == structure);
        if (f.ProjetId is { } projet) q = q.Where(i => i.ProjetId == projet);
        if (f.Etat is { } etat) q = q.Where(i => i.Etat == etat);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(i => i.Code.Contains(r) || i.Designation.Contains(r));
        }

        var total = await q.CountAsync();
        var valeur = await q.SumAsync(i => (decimal?)i.ValeurAcquisition) ?? 0;
        var items = await q.OrderBy(i => i.Site.Code).ThenBy(i => i.Code).Skip((page - 1) * taille).Take(taille)
            .Select(i => new LigneImmobilisation(i.Id, i.Code, i.Designation, i.Site.Nom,
                i.Structure != null ? i.Structure.Code : null, i.Projet != null ? i.Projet.Code : null,
                i.CompteComptable.Numero + " — " + i.CompteComptable.Libelle, i.DateAcquisition, i.ValeurAcquisition, i.Etat))
            .ToListAsync();
        return (items, total, valeur);
    }

    public async Task<List<InventaireSite>> ParSiteAsync()
    {
        if (await _acces.LecteurAsync(Axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var sites = await ctx.Immobilisations.AsNoTracking()
            .Where(i => i.Etat != EtatImmobilisation.CedeReforme)
            .GroupBy(i => new { i.SiteId, i.Site.Nom, i.Site.TypeSite })
            .Select(g => new
            {
                g.Key.SiteId, g.Key.Nom, g.Key.TypeSite, Nb = g.Count(), Valeur = g.Sum(i => i.ValeurAcquisition),
                HorsService = g.Count(i => i.Etat == EtatImmobilisation.HorsService)
            })
            .ToListAsync();
        return sites.Select(s => new InventaireSite(s.SiteId, s.Nom, s.TypeSite, s.Nb, s.Valeur, s.HorsService))
                    .OrderBy(s => s.Site, StringComparer.CurrentCulture).ToList();
    }

    public async Task<SaisieImmobilisation?> ObtenirSaisieAsync(Ulid immobilisationId)
    {
        if (await _acces.LecteurAsync(Axe) == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Immobilisations.AsNoTracking().Where(i => i.Id == immobilisationId)
            .Select(i => new SaisieImmobilisation
            {
                Id = i.Id, Code = i.Code, Designation = i.Designation, ProjetId = i.ProjetId, SiteId = i.SiteId,
                StructureId = i.StructureId, CompteComptableId = i.CompteComptableId, DateAcquisition = i.DateAcquisition,
                ValeurAcquisition = i.ValeurAcquisition, Etat = i.Etat
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<ElementListe>> ListerComptesAsync()
    {
        if (await _acces.LecteurAsync(Axe) == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ComptesComptables.AsNoTracking()
            .Where(c => c.EstActif && c.EstImmobilisation)
            .OrderBy(c => c.Numero)
            .Select(c => new ElementListe(c.Id, c.Numero + " — " + c.Libelle))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieImmobilisation s)
    {
        var acteurId = await _acces.SaisisseurAsync(Axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(Axe));

        var code = s.Code?.Trim() ?? "";
        var designation = s.Designation?.Trim() ?? "";
        if (code.Length == 0) return (false, "Le code inventaire est obligatoire.");
        if (code.Length > 30) return (false, "Code inventaire : 30 caractères au plus.");
        if (designation.Length == 0) return (false, "La désignation est obligatoire.");
        if (designation.Length > 300) return (false, "Désignation : 300 caractères au plus.");
        if (s.SiteId is not { } siteId) return (false, "Choisissez le site.");
        if (s.CompteComptableId is not { } compteId) return (false, "Choisissez le compte d'immobilisation.");
        if (s.DateAcquisition is not { } dateAcquisition) return (false, "Indiquez la date d'acquisition.");
        if (s.ValeurAcquisition < 0 || !CalculsExecution.EstMontantValide(s.ValeurAcquisition))
            return (false, "La valeur d'acquisition est un montant positif ou nul, à 2 décimales au plus.");
        if (!Enum.IsDefined(s.Etat)) return (false, "État inconnu.");

        await using var ctx = await _factory.CreateDbContextAsync();
        if (!await ctx.Sites.AnyAsync(x => x.Id == siteId && x.EstActif)) return (false, "Site introuvable ou inactif.");
        if (s.StructureId is { } st && !await ctx.Structures.AnyAsync(x => x.Id == st && x.EstActif)) return (false, "Service introuvable ou inactif.");
        if (!await ctx.ComptesComptables.AnyAsync(x => x.Id == compteId && x.EstActif && x.EstImmobilisation))
            return (false, "Le compte doit être un compte d'immobilisation actif du plan comptable.");
        if (s.ProjetId is { } projetId && !await ctx.Projets.AnyAsync(p => p.Id == projetId && p.Axe.Code == Axe.Code))
            return (false, "Le projet d'acquisition doit être un projet de l'axe Investissements.");

        var axeId = await AccesAxes.AxeIdAsync(ctx, Axe);
        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: axeId, StructureId: s.StructureId, SiteId: siteId));
        if (refus is not null) return (false, refus);

        if (await ctx.Immobilisations.AnyAsync(i => i.Id != s.Id && i.Code == code))
            return (false, $"Le code inventaire {code} est déjà attribué.");

        Immobilisation immobilisation;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            immobilisation = new Immobilisation { CreePar = acteurId };
            ctx.Immobilisations.Add(immobilisation);
        }
        else
        {
            var existante = await ctx.Immobilisations.FirstOrDefaultAsync(i => i.Id == s.Id);
            if (existante is null) return (false, "Immobilisation introuvable.");
            // Déplacer un bien exige aussi le périmètre du site ou du service qu'il quitte.
            if ((existante.SiteId != siteId || existante.StructureId != s.StructureId)
                && await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: axeId, StructureId: existante.StructureId, SiteId: existante.SiteId)) is { } refusOrigine)
                return (false, refusOrigine);
            immobilisation = existante;
            avant = new { immobilisation.Code, immobilisation.SiteId, immobilisation.ValeurAcquisition, immobilisation.Etat };
        }

        immobilisation.Code = code;
        immobilisation.Designation = designation;
        immobilisation.ProjetId = s.ProjetId;
        immobilisation.SiteId = siteId;
        immobilisation.StructureId = s.StructureId;
        immobilisation.CompteComptableId = compteId;
        immobilisation.DateAcquisition = dateAcquisition;
        immobilisation.ValeurAcquisition = s.ValeurAcquisition;
        immobilisation.Etat = s.Etat;
        immobilisation.ModifiePar = acteurId;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (AccesExecution.EstConflitUnicite(ex))
        {
            return (false, $"Le code inventaire {code} est déjà attribué.");
        }

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesAxes.ModuleAudit,
            nameof(Immobilisation), immobilisation.Id.ToString(), avant,
            new { immobilisation.Code, immobilisation.SiteId, immobilisation.ValeurAcquisition, immobilisation.Etat });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid immobilisationId)
    {
        var acteurId = await _acces.SaisisseurAsync(Axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(Axe));

        await using var ctx = await _factory.CreateDbContextAsync();
        var immobilisation = await ctx.Immobilisations.FirstOrDefaultAsync(i => i.Id == immobilisationId);
        if (immobilisation is null) return (false, "Immobilisation introuvable.");
        var axeId = await AccesAxes.AxeIdAsync(ctx, Axe);
        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: axeId, StructureId: immobilisation.StructureId, SiteId: immobilisation.SiteId));
        if (refus is not null) return (false, refus);

        immobilisation.EstSupprime = true;
        immobilisation.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesAxes.ModuleAudit, nameof(Immobilisation),
            immobilisation.Id.ToString(), new { immobilisation.Code, immobilisation.Designation }, null);
        return (true, null);
    }
}
