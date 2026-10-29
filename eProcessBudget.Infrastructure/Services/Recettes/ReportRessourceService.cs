using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Recettes;

public sealed record LigneReportRessource(Ulid Id, Ulid NatureRecetteId, string Nature, decimal Montant, bool EstPositionne, string? Commentaire, decimal? MontantPrevu = null);

public sealed class SaisieReportRessource
{
    public Ulid Id { get; set; }
    public Ulid ExerciceId { get; set; }
    public Ulid? NatureRecetteId { get; set; }
    public decimal Montant { get; set; }

    /// <summary>Montant inscrit au budget (prévision ajustée du compte sur chiffres, C.2.9) ; facultatif.</summary>
    public decimal? MontantPrevu { get; set; }

    /// <summary>Ressource positionnée sur un compte bancaire ; sinon acquise mais non positionnée.</summary>
    public bool EstPositionne { get; set; }

    public string? Commentaire { get; set; }
}

/// <summary>
/// Reports de ressources antérieures (cahier fonctionnel § 3.2.8 et 3.9.2, technique § 4.6) :
/// trésorerie sans déduction d'encours, fournisseurs débiteurs, taxes affectées et recettes
/// propres antérieures, avoirs sur l'État à recouvrer. Ils s'ajoutent aux ressources de
/// l'exercice de destination ; ceux qui ne sont pas positionnés sur un compte bancaire
/// restent des ressources acquises à mobiliser.
/// </summary>
public interface IReportRessourceService
{
    Task<List<LigneReportRessource>> ListerAsync(Ulid exerciceId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieReportRessource saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid reportId);

    /// <summary>Natures de recettes de la catégorie « Report de ressources antérieures », pour un choix.</summary>
    Task<List<ElementListe>> ListerNaturesAsync();
}

public sealed class ReportRessourceService : IReportRessourceService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesRecettes _acces;
    private readonly IAuditService _audit;

    public ReportRessourceService(IDbContextFactory<AppDbContext> factory, AccesRecettes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<LigneReportRessource>> ListerAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ReportsRessources.AsNoTracking().Where(r => r.ExerciceId == exerciceId)
            .OrderBy(r => r.NatureRecette.Code).ThenBy(r => r.DateCreation)
            .Select(r => new LigneReportRessource(r.Id, r.NatureRecetteId, r.NatureRecette.Code + " — " + r.NatureRecette.Libelle,
                r.Montant, r.EstPositionne, r.Commentaire, r.MontantPrevu))
            .ToListAsync();
    }

    public async Task<List<ElementListe>> ListerNaturesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.NaturesRecette.AsNoTracking()
            .Where(n => n.EstActif && n.CategorieRecette == CategorieRecette.ReportRessourcesAnterieures)
            .OrderBy(n => n.Code)
            .Select(n => new ElementListe(n.Id, n.Code + " — " + n.Libelle))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieReportRessource s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PrevisionRecetteSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("saisir les reports de ressources"));

        if (s.NatureRecetteId is not { } natureId) return (false, "Choisissez la nature du report.");
        if (s.Montant <= 0 || !CalculsExecution.EstMontantValide(s.Montant)) return (false, "Le montant est positif, à 2 décimales au plus.");
        if (s.MontantPrevu is { } prevu && (prevu < 0 || !CalculsExecution.EstMontantValide(prevu))) return (false, "Le montant prévu est positif, à 2 décimales au plus.");
        var commentaire = string.IsNullOrWhiteSpace(s.Commentaire) ? null : s.Commentaire.Trim();
        if (commentaire?.Length > 500) return (false, "Commentaire : 500 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var exercice = await ctx.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Id == s.ExerciceId);
        if (exercice is null) return (false, "Exercice introuvable.");
        if (exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé : plus aucune saisie.");
        if (!await ctx.NaturesRecette.AnyAsync(n => n.Id == natureId && n.EstActif && n.CategorieRecette == CategorieRecette.ReportRessourcesAnterieures))
            return (false, "La nature doit être une nature active de la catégorie « Report de ressources antérieures ».");

        var axeRecettes = await ctx.Axes.AsNoTracking().Where(a => a.TypeAxe == TypeAxe.Recette).Select(a => (Ulid?)a.Id).FirstOrDefaultAsync();
        var refus = await _acces.PerimetreAsync(acteurId, axeRecettes, null);
        if (refus is not null) return (false, refus);

        ReportRessource report;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            report = new ReportRessource { ExerciceId = s.ExerciceId, CreePar = acteurId };
            ctx.ReportsRessources.Add(report);
        }
        else
        {
            var existant = await ctx.ReportsRessources.FirstOrDefaultAsync(r => r.Id == s.Id);
            if (existant is null) return (false, "Report introuvable.");
            if (existant.ExerciceId != s.ExerciceId) return (false, "Un report ne change pas d'exercice.");
            report = existant;
            avant = new { report.NatureRecetteId, report.Montant, report.EstPositionne };
        }

        report.NatureRecetteId = natureId;
        report.Montant = s.Montant;
        report.MontantPrevu = s.MontantPrevu;
        report.EstPositionne = s.EstPositionne;
        report.Commentaire = commentaire;
        report.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesRecettes.ModuleAudit,
            nameof(ReportRessource), report.Id.ToString(), avant, new { report.NatureRecetteId, report.Montant, report.EstPositionne });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid reportId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PrevisionRecetteSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("supprimer un report de ressources"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var report = await ctx.ReportsRessources.Include(r => r.Exercice).FirstOrDefaultAsync(r => r.Id == reportId);
        if (report is null) return (false, "Report introuvable.");
        if (report.Exercice.Statut == StatutExercice.Cloture) return (false, "L'exercice est clôturé.");
        var axeRecettes = await ctx.Axes.AsNoTracking().Where(a => a.TypeAxe == TypeAxe.Recette).Select(a => (Ulid?)a.Id).FirstOrDefaultAsync();
        var refus = await _acces.PerimetreAsync(acteurId, axeRecettes, null);
        if (refus is not null) return (false, refus);

        report.EstSupprime = true;
        report.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesRecettes.ModuleAudit, nameof(ReportRessource),
            report.Id.ToString(), new { report.NatureRecetteId, report.Montant }, null);
        return (true, null);
    }
}
