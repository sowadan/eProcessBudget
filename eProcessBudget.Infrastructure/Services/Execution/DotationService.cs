using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>Saisie d'une dotation : ligne × projet × site × service × entité, et sa prévision initiale.</summary>
public sealed class SaisieDotation
{
    public Ulid Id { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid? LigneBudgetaireId { get; set; }
    public Ulid? ProjetId { get; set; }
    public Ulid? SiteId { get; set; }
    public Ulid? StructureId { get; set; }
    public Ulid? TiersId { get; set; }
    public decimal MontantInitial { get; set; }
}

/// <summary>
/// Prévisions initiales (cahier fonctionnel § 3.4.2).
///
/// La prévision initiale se saisit librement tant que l'exercice est en préparation ; elle
/// est figée à son ouverture (dotations validées par <c>ExerciceService.OuvrirAsync</c>) et
/// n'évolue plus ensuite que par des ajustements motivés. Une dotation créée en cours
/// d'exercice part de 0 et s'alimente par ajustement.
/// </summary>
public interface IDotationService
{
    Task<SaisieDotation?> ObtenirSaisieAsync(Ulid dotationId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieDotation saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid dotationId);

    /// <summary>Lignes de dépense actives, pour le choix d'une dotation.</summary>
    Task<List<ElementListe>> ListerLignesAsync();

    /// <summary>Projets non annulés, pour le choix d'une dotation.</summary>
    Task<List<ElementListe>> ListerProjetsAsync();
}

public sealed class DotationService : IDotationService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesExecution _acces;
    private readonly IAuditService _audit;

    public DotationService(IDbContextFactory<AppDbContext> factory, AccesExecution acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<SaisieDotation?> ObtenirSaisieAsync(Ulid dotationId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Dotations.AsNoTracking().Where(d => d.Id == dotationId)
            .Select(d => new SaisieDotation
            {
                Id = d.Id, ExerciceId = d.ExerciceId, LigneBudgetaireId = d.LigneBudgetaireId, ProjetId = d.ProjetId,
                SiteId = d.SiteId, StructureId = d.StructureId, TiersId = d.TiersId, MontantInitial = d.MontantInitial
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieDotation s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.DotationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("saisir les dotations"));

        if (s.LigneBudgetaireId is not { } ligneId) return (false, "Choisissez la ligne budgétaire.");
        if (s.MontantInitial < 0 || !CalculsExecution.EstMontantValide(s.MontantInitial))
            return (false, "La prévision initiale est un montant positif ou nul, à 2 décimales au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Id == s.ExerciceId);
        if (exercice is null) return (false, "Exercice introuvable.");
        if (exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé : plus aucune saisie.");
        var enPreparation = exercice.Statut == StatutExercice.EnPreparation;

        var ligne = await ctx.LignesBudgetaires.AsNoTracking().Where(l => l.Id == ligneId)
            .Select(l => new { l.EstActif, l.Sens, l.SousRubrique.Rubrique.AxeId }).FirstOrDefaultAsync();
        if (ligne is null || !ligne.EstActif) return (false, "Ligne budgétaire introuvable ou inactive.");
        if (ligne.Sens != SensLigne.Depense) return (false, "Une dotation porte sur une ligne de dépense ; les recettes se prévoient dans le module Recettes.");

        var refEnErreur = await ReferencesAsync(ctx, s);
        if (refEnErreur is not null) return (false, refEnErreur);
        if (s.ProjetId is { } projetId && await ctx.Projets.Where(p => p.Id == projetId).Select(p => p.AxeId).FirstAsync() != ligne.AxeId)
            return (false, "Le projet relève d'un autre axe que la ligne budgétaire.");

        var refus = await _acces.PerimetreAsync(acteurId, new Plateforme.CiblePerimetre(AxeId: ligne.AxeId, StructureId: s.StructureId, SiteId: s.SiteId));
        if (refus is not null) return (false, refus);

        // Une seule dotation par imputation (ligne, projet, site, service, entité) et par exercice.
        var doublon = await ctx.Dotations.AnyAsync(d => d.Id != s.Id && d.ExerciceId == s.ExerciceId && d.LigneBudgetaireId == ligneId
            && d.ProjetId == s.ProjetId && d.SiteId == s.SiteId && d.StructureId == s.StructureId && d.TiersId == s.TiersId);
        if (doublon) return (false, "Une dotation existe déjà pour cette ligne et cette imputation sur l'exercice.");

        Dotation dotation;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            // En cours d'exercice : la dotation naît validée, à 0, et s'alimente par ajustement.
            if (!enPreparation && s.MontantInitial != 0)
                return (false, "L'exercice est ouvert : la prévision initiale est figée. Créez la dotation à 0 et alimentez-la par un ajustement.");
            dotation = new Dotation
            {
                ExerciceId = s.ExerciceId, CreePar = acteurId,
                Statut = enPreparation ? StatutSaisie.Brouillon : StatutSaisie.Valide
            };
            ctx.Dotations.Add(dotation);
        }
        else
        {
            var existante = await ctx.Dotations.FirstOrDefaultAsync(d => d.Id == s.Id);
            if (existante is null) return (false, "Dotation introuvable.");
            if (existante.ExerciceId != s.ExerciceId) return (false, "Une dotation ne change pas d'exercice.");
            if (existante.Statut != StatutSaisie.Brouillon)
                return (false, "La prévision initiale est figée depuis l'ouverture de l'exercice : passez par un ajustement.");
            // Changer l'imputation exige aussi le périmètre de celle qu'elle quitte.
            if (await _acces.PerimetreAsync(ctx, acteurId, existante.Id) is { } refusOrigine) return (false, refusOrigine);
            dotation = existante;
            avant = new { dotation.LigneBudgetaireId, dotation.MontantInitial };
        }

        dotation.LigneBudgetaireId = ligneId;
        dotation.ProjetId = s.ProjetId;
        dotation.SiteId = s.SiteId;
        dotation.StructureId = s.StructureId;
        dotation.TiersId = s.TiersId;
        dotation.MontantInitial = s.MontantInitial;
        dotation.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesExecution.ModuleAudit,
            nameof(Dotation), dotation.Id.ToString(), avant, new { dotation.LigneBudgetaireId, dotation.MontantInitial });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid dotationId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.DotationSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("supprimer une dotation"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var dotation = await ctx.Dotations.Include(d => d.Exercice).FirstOrDefaultAsync(d => d.Id == dotationId);
        if (dotation is null) return (false, "Dotation introuvable.");
        if (dotation.Exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé.");

        var refus = await _acces.PerimetreAsync(ctx, acteurId, dotationId);
        if (refus is not null) return (false, refus);

        // Règle § 3.4 : on ne supprime pas ce qui a des pièces rattachées.
        var utilisee = await ctx.Ajustements.AnyAsync(a => a.DotationId == dotationId)
                    || await ctx.Engagements.AnyAsync(e => e.DotationId == dotationId)
                    || await ctx.Reports.AnyAsync(r => r.DotationCibleId == dotationId || r.DotationSourceId == dotationId);
        if (utilisee) return (false, "Cette dotation porte des ajustements, des engagements ou des reports : elle ne peut pas être supprimée.");
        if (dotation.Statut == StatutSaisie.Valide && dotation.MontantInitial != 0)
            return (false, "La prévision initiale de cette dotation est figée : elle ne peut plus être supprimée.");

        dotation.EstSupprime = true;
        dotation.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesExecution.ModuleAudit, nameof(Dotation),
            dotation.Id.ToString(), new { dotation.LigneBudgetaireId, dotation.MontantInitial }, null);
        return (true, null);
    }

    public async Task<List<ElementListe>> ListerLignesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.LignesBudgetaires.AsNoTracking()
            .Where(l => l.EstActif && l.Sens == SensLigne.Depense)
            .OrderBy(l => l.Code)
            .Select(l => new ElementListe(l.Id, l.Code + " — " + l.Libelle))
            .ToListAsync();
    }

    public async Task<List<ElementListe>> ListerProjetsAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Projets.AsNoTracking()
            .Where(p => p.Statut != StatutProjet.Annule)
            .OrderBy(p => p.Code)
            .Select(p => new ElementListe(p.Id, p.Code + " — " + p.Intitule))
            .ToListAsync();
    }

    /// <summary>Projet, site, service et entité existent dans l'organisme et sont utilisables.</summary>
    private static async Task<string?> ReferencesAsync(AppDbContext ctx, SaisieDotation s)
    {
        if (s.ProjetId is { } p && !await ctx.Projets.AnyAsync(x => x.Id == p && x.Statut != StatutProjet.Annule))
            return "Projet introuvable ou annulé.";
        if (s.SiteId is { } site && !await ctx.Sites.AnyAsync(x => x.Id == site && x.EstActif))
            return "Site introuvable ou inactif.";
        if (s.StructureId is { } st && !await ctx.Structures.AnyAsync(x => x.Id == st && x.EstActif))
            return "Service introuvable ou inactif.";
        if (s.TiersId is { } t && !await ctx.Tiers.AnyAsync(x => x.Id == t && x.EstActif))
            return "Entité introuvable ou inactive.";
        return null;
    }
}
