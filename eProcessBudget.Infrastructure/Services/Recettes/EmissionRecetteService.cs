using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Recettes;

public sealed record LigneEmission(
    Ulid Id, Ulid PrevisionRecetteId, string Prevision, DateOnly Date, int Mois, decimal Montant, string? Reference);

public sealed class SaisieEmission
{
    public Ulid Id { get; set; }
    public Ulid? PrevisionRecetteId { get; set; }
    public DateOnly? Date { get; set; }
    public decimal Montant { get; set; }

    /// <summary>Numéro de la facture ou du titre de recette.</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// Factures émises (titres de recette, droits constatés) sur les prévisions de recette
/// (réponse du métier C.2.9) : elles alimentent la colonne « factures émises » du compte sur
/// chiffres. % d'exécution = émis / prévision ; % de recouvrement = encaissé / émis ; reste à
/// recouvrer = émis − encaissé.
///
/// Mêmes règles que les encaissements : exercice ouvert, mois non clos, droit de saisie des
/// encaissements et périmètre de la prévision. Une facture saisie est validée d'emblée.
/// </summary>
public interface IEmissionRecetteService
{
    Task<(List<LigneEmission> Items, int Total, decimal Montant)> ListerAsync(FiltreEncaissements filtre, int page = 1, int taille = 20);
    Task<SaisieEmission?> ObtenirSaisieAsync(Ulid emissionId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieEmission saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid emissionId);
}

public sealed class EmissionRecetteService : IEmissionRecetteService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesRecettes _acces;
    private readonly IAuditService _audit;

    public EmissionRecetteService(IDbContextFactory<AppDbContext> factory, AccesRecettes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<(List<LigneEmission> Items, int Total, decimal Montant)> ListerAsync(FiltreEncaissements f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0, 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.EmissionsRecettes.AsNoTracking().Where(e => e.ExerciceId == f.ExerciceId && e.Statut == StatutSaisie.Valide);
        if (f.PrevisionRecetteId is { } prevision) q = q.Where(e => e.PrevisionRecetteId == prevision);
        if (f.PeriodeId is { } periode) q = q.Where(e => e.PeriodeId == periode);
        if (f.SiteId is { } site) q = q.Where(e => e.PrevisionRecette.SiteId == site);
        if (f.Categorie is { } categorie) q = q.Where(e => e.PrevisionRecette.NatureRecette.CategorieRecette == categorie);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(e => (e.Reference != null && e.Reference.Contains(r)) || e.PrevisionRecette.LigneBudgetaire.Code.Contains(r)
                          || e.PrevisionRecette.NatureRecette.Libelle.Contains(r));
        }

        var total = await q.CountAsync();
        var montant = await q.SumAsync(e => (decimal?)e.Montant) ?? 0;
        var items = await q.OrderByDescending(e => e.DateEmission).ThenByDescending(e => e.DateCreation)
            .Skip((page - 1) * taille).Take(taille)
            .Select(e => new LigneEmission(e.Id, e.PrevisionRecetteId,
                e.PrevisionRecette.LigneBudgetaire.Code + " — " + e.PrevisionRecette.NatureRecette.Libelle
                    + (e.PrevisionRecette.Site != null ? " · " + e.PrevisionRecette.Site.Nom : ""),
                e.DateEmission, e.Periode.Numero, e.Montant, e.Reference))
            .ToListAsync();
        return (items, total, montant);
    }

    public async Task<SaisieEmission?> ObtenirSaisieAsync(Ulid emissionId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.EmissionsRecettes.AsNoTracking().Where(e => e.Id == emissionId)
            .Select(e => new SaisieEmission { Id = e.Id, PrevisionRecetteId = e.PrevisionRecetteId, Date = e.DateEmission, Montant = e.Montant, Reference = e.Reference })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieEmission s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EncaissementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("saisir les factures émises"));

        if (s.PrevisionRecetteId is not { } previsionId) return (false, "Choisissez la prévision (nature, site) facturée.");
        if (s.Date is not { } date) return (false, "Indiquez la date d'émission.");
        if (s.Montant <= 0 || !CalculsExecution.EstMontantValide(s.Montant)) return (false, "Le montant est positif, à 2 décimales au plus.");
        var reference = string.IsNullOrWhiteSpace(s.Reference) ? null : s.Reference.Trim();
        if (reference?.Length > 100) return (false, "Référence : 100 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var prevision = await ctx.PrevisionsRecettes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == previsionId);
        if (prevision is null) return (false, "Prévision introuvable.");
        var (exercice, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, prevision.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var erreurDate = await AccesExecution.DateSaisissableAsync(ctx, exercice!, date, "d'émission");
        if (erreurDate is not null) return (false, erreurDate);
        var periodeId = await ctx.Periodes.Where(p => p.ExerciceId == exercice!.Id && p.TypePeriode == TypePeriode.Mois
                                                   && p.DateDebut <= date && p.DateFin >= date)
                                          .Select(p => (Ulid?)p.Id).FirstOrDefaultAsync();
        if (periodeId is null) return (false, "Aucun mois de l'exercice ne correspond à cette date.");

        var refus = await _acces.PerimetrePrevisionAsync(ctx, acteurId, previsionId);
        if (refus is not null) return (false, refus);

        EmissionRecette emission;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            emission = new EmissionRecette { ExerciceId = prevision.ExerciceId, CreePar = acteurId };
            ctx.EmissionsRecettes.Add(emission);
        }
        else
        {
            var existante = await ctx.EmissionsRecettes.Include(e => e.Periode).FirstOrDefaultAsync(e => e.Id == s.Id);
            if (existante is null) return (false, "Facture introuvable.");
            if (existante.ExerciceId != prevision.ExerciceId) return (false, "Une facture émise ne change pas d'exercice.");
            if (existante.Periode.EstCloturee) return (false, "Le mois de cette facture est clos : elle ne se modifie plus.");
            // Changer de prévision exige aussi le périmètre de celle qu'elle quitte.
            if (existante.PrevisionRecetteId != previsionId
                && await _acces.PerimetrePrevisionAsync(ctx, acteurId, existante.PrevisionRecetteId) is { } refusOrigine)
                return (false, refusOrigine);
            emission = existante;
            avant = new { emission.PrevisionRecetteId, emission.DateEmission, emission.Montant, emission.Reference };
        }

        emission.PrevisionRecetteId = previsionId;
        emission.PeriodeId = periodeId.Value;
        emission.DateEmission = date;
        emission.Montant = s.Montant;
        emission.Reference = reference;
        emission.Statut = StatutSaisie.Valide;
        emission.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesRecettes.ModuleAudit,
            nameof(EmissionRecette), emission.Id.ToString(), avant,
            new { emission.PrevisionRecetteId, emission.DateEmission, emission.Montant, emission.Reference });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid emissionId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EncaissementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("supprimer une facture émise"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var emission = await ctx.EmissionsRecettes.Include(e => e.Periode).FirstOrDefaultAsync(e => e.Id == emissionId);
        if (emission is null) return (false, "Facture introuvable.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, emission.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        if (emission.Periode.EstCloturee) return (false, "Le mois de cette facture est clos : elle ne se supprime plus.");
        var refus = await _acces.PerimetrePrevisionAsync(ctx, acteurId, emission.PrevisionRecetteId);
        if (refus is not null) return (false, refus);

        emission.EstSupprime = true;
        emission.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesRecettes.ModuleAudit, nameof(EmissionRecette),
            emission.Id.ToString(), new { emission.PrevisionRecetteId, emission.DateEmission, emission.Montant }, null);
        return (true, null);
    }
}
