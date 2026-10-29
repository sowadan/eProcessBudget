using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Recettes;

/// <summary>Critères du journal des encaissements d'un exercice.</summary>
public sealed class FiltreEncaissements
{
    public Ulid ExerciceId { get; set; }
    public Ulid? PrevisionRecetteId { get; set; }

    /// <summary>Mois (période mensuelle).</summary>
    public Ulid? PeriodeId { get; set; }

    public Ulid? SiteId { get; set; }
    public CategorieRecette? Categorie { get; set; }

    /// <summary>Positionnés sur un compte bancaire (true), non positionnés (false), tous (null).</summary>
    public bool? EstPositionne { get; set; }

    /// <summary>Référence de la pièce, code de ligne ou de nature.</summary>
    public string? Recherche { get; set; }
}

public sealed record LigneEncaissement(
    Ulid Id, Ulid PrevisionRecetteId, string Prevision, DateOnly Date, int Mois, decimal Montant, bool EstPositionne, string? Compte, string? Reference);

/// <summary>Une prévision et ses encaissements validés, mois par mois (index 0 = premier mois de l'exercice).</summary>
public sealed record LigneGrille(
    Ulid PrevisionId, string LigneCode, string Nature, CategorieRecette Categorie, string? Site, decimal PrevisionRetenue, decimal[] Mois)
{
    public decimal Total => Mois.Sum();
    public decimal? TauxRecouvrement => CalculsExecution.Taux(Total, PrevisionRetenue);
}

public sealed class SaisieEncaissement
{
    public Ulid Id { get; set; }
    public Ulid? PrevisionRecetteId { get; set; }
    public DateOnly? Date { get; set; }
    public decimal Montant { get; set; }

    /// <summary>Compte crédité : l'encaissement est alors positionné sur un compte bancaire.</summary>
    public Ulid? CompteBancaireId { get; set; }

    public string? Reference { get; set; }
}

/// <summary>
/// Saisie mensuelle des recettes collectées (cahier fonctionnel § 3.9.2 et 4.2, technique
/// § 4.6) : chaque encaissement porte sur une prévision (nature, site), une date de
/// l'exercice ouvert dont le mois n'est pas clos, et un montant. Crédité sur un compte
/// bancaire, il est « positionné » ; sinon il compte parmi les ressources acquises mais non
/// positionnées.
///
/// Un encaissement saisi est validé d'emblée et compte dans la situation. Il se corrige ou se
/// supprime tant que son mois n'est pas clos ; ensuite il est figé.
/// </summary>
public interface IEncaissementService
{
    /// <summary>Grille prévision × mois de l'exercice (500 prévisions au plus) et totaux par mois.</summary>
    Task<(List<LigneGrille> Lignes, decimal[] TotauxMois)> GrilleAsync(FiltrePrevisions filtre);

    Task<(List<LigneEncaissement> Items, int Total, decimal Montant)> ListerAsync(FiltreEncaissements filtre, int page = 1, int taille = 20);
    Task<SaisieEncaissement?> ObtenirSaisieAsync(Ulid encaissementId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieEncaissement saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid encaissementId);

    /// <summary>Comptes bancaires actifs de l'organisme, pour un choix.</summary>
    Task<List<ElementListe>> ListerComptesAsync();
}

public sealed class EncaissementService : IEncaissementService
{
    private const int LignesGrilleMax = 500;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesRecettes _acces;
    private readonly IAuditService _audit;

    public EncaissementService(IDbContextFactory<AppDbContext> factory, AccesRecettes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<(List<LigneGrille> Lignes, decimal[] TotauxMois)> GrilleAsync(FiltrePrevisions f)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], new decimal[12]);

        await using var ctx = await _factory.CreateDbContextAsync();
        var previsions = await PrevisionRecetteService.Projeter(PrevisionRecetteService.Filtrer(ctx.PrevisionsRecettes.AsNoTracking(), f)
                .OrderBy(p => p.NatureRecette.Code).ThenBy(p => p.Site!.Code).ThenBy(p => p.Id).Take(LignesGrilleMax))
            .ToListAsync();
        var ids = previsions.Select(p => p.Id).ToList();
        var cumuls = await ctx.Encaissements.AsNoTracking()
            .Where(e => ids.Contains(e.PrevisionRecetteId) && e.Statut == StatutSaisie.Valide)
            .GroupBy(e => new { e.PrevisionRecetteId, e.Periode.Numero })
            .Select(g => new { g.Key.PrevisionRecetteId, g.Key.Numero, Montant = g.Sum(e => e.Montant) })
            .ToListAsync();

        var lignes = previsions.Select(p =>
        {
            var mois = new decimal[12];
            foreach (var c in cumuls.Where(c => c.PrevisionRecetteId == p.Id && c.Numero is >= 1 and <= 12)) mois[c.Numero - 1] = c.Montant;
            return new LigneGrille(p.Id, p.LigneCode, p.Nature, p.Categorie, p.Site, p.Montants.PrevisionRetenue, mois);
        }).ToList();
        var totaux = Enumerable.Range(0, 12).Select(i => lignes.Sum(l => l.Mois[i])).ToArray();
        return (lignes, totaux);
    }

    public async Task<(List<LigneEncaissement> Items, int Total, decimal Montant)> ListerAsync(FiltreEncaissements f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0, 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Encaissements.AsNoTracking().Where(e => e.ExerciceId == f.ExerciceId && e.Statut == StatutSaisie.Valide);
        if (f.PrevisionRecetteId is { } prevision) q = q.Where(e => e.PrevisionRecetteId == prevision);
        if (f.PeriodeId is { } periode) q = q.Where(e => e.PeriodeId == periode);
        if (f.SiteId is { } site) q = q.Where(e => e.PrevisionRecette.SiteId == site);
        if (f.Categorie is { } categorie) q = q.Where(e => e.PrevisionRecette.NatureRecette.CategorieRecette == categorie);
        if (f.EstPositionne is { } positionne) q = q.Where(e => e.EstPositionne == positionne);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(e => (e.Reference != null && e.Reference.Contains(r)) || e.PrevisionRecette.LigneBudgetaire.Code.Contains(r)
                          || e.PrevisionRecette.NatureRecette.Libelle.Contains(r));
        }

        var total = await q.CountAsync();
        var montant = await q.SumAsync(e => (decimal?)e.Montant) ?? 0;
        var items = await q.OrderByDescending(e => e.DateEncaissement).ThenByDescending(e => e.DateCreation)
            .Skip((page - 1) * taille).Take(taille)
            .Select(e => new LigneEncaissement(e.Id, e.PrevisionRecetteId,
                e.PrevisionRecette.LigneBudgetaire.Code + " — " + e.PrevisionRecette.NatureRecette.Libelle
                    + (e.PrevisionRecette.Site != null ? " · " + e.PrevisionRecette.Site.Nom : ""),
                e.DateEncaissement, e.Periode.Numero, e.Montant, e.EstPositionne,
                e.CompteBancaire != null ? e.CompteBancaire.Banque + " " + e.CompteBancaire.NumeroCompte : null, e.Reference))
            .ToListAsync();
        return (items, total, montant);
    }

    public async Task<SaisieEncaissement?> ObtenirSaisieAsync(Ulid encaissementId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Encaissements.AsNoTracking().Where(e => e.Id == encaissementId)
            .Select(e => new SaisieEncaissement
            {
                Id = e.Id, PrevisionRecetteId = e.PrevisionRecetteId, Date = e.DateEncaissement, Montant = e.Montant,
                CompteBancaireId = e.CompteBancaireId, Reference = e.Reference
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<ElementListe>> ListerComptesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ComptesBancaires.AsNoTracking().Where(c => c.EstActif).OrderBy(c => c.Banque).ThenBy(c => c.NumeroCompte)
            .Select(c => new ElementListe(c.Id, c.Banque + " " + c.NumeroCompte + " — " + c.Intitule)).ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieEncaissement s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EncaissementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("saisir les encaissements"));

        if (s.PrevisionRecetteId is not { } previsionId) return (false, "Choisissez la prévision (nature, site) encaissée.");
        if (s.Date is not { } date) return (false, "Indiquez la date de l'encaissement.");
        if (s.Montant <= 0 || !CalculsExecution.EstMontantValide(s.Montant)) return (false, "Le montant est positif, à 2 décimales au plus.");
        var reference = string.IsNullOrWhiteSpace(s.Reference) ? null : s.Reference.Trim();
        if (reference?.Length > 100) return (false, "Référence : 100 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var prevision = await ctx.PrevisionsRecettes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == previsionId);
        if (prevision is null) return (false, "Prévision introuvable.");
        var (exercice, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, prevision.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var erreurDate = await AccesExecution.DateSaisissableAsync(ctx, exercice!, date, "de l'encaissement");
        if (erreurDate is not null) return (false, erreurDate);
        var periodeId = await ctx.Periodes.Where(p => p.ExerciceId == exercice!.Id && p.TypePeriode == TypePeriode.Mois
                                                   && p.DateDebut <= date && p.DateFin >= date)
                                          .Select(p => (Ulid?)p.Id).FirstOrDefaultAsync();
        if (periodeId is null) return (false, "Aucun mois de l'exercice ne correspond à cette date.");
        if (s.CompteBancaireId is { } compte && !await ctx.ComptesBancaires.AnyAsync(c => c.Id == compte && c.EstActif))
            return (false, "Compte bancaire introuvable ou inactif.");

        var refus = await _acces.PerimetrePrevisionAsync(ctx, acteurId, previsionId);
        if (refus is not null) return (false, refus);

        Encaissement encaissement;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            encaissement = new Encaissement { ExerciceId = prevision.ExerciceId, CreePar = acteurId };
            ctx.Encaissements.Add(encaissement);
        }
        else
        {
            var existant = await ctx.Encaissements.Include(e => e.Periode).FirstOrDefaultAsync(e => e.Id == s.Id);
            if (existant is null) return (false, "Encaissement introuvable.");
            if (existant.ExerciceId != prevision.ExerciceId) return (false, "Un encaissement ne change pas d'exercice.");
            if (existant.Periode.EstCloturee) return (false, "Le mois de cet encaissement est clos : il ne se modifie plus.");
            // Changer de prévision exige aussi le périmètre de celle qu'il quitte.
            if (existant.PrevisionRecetteId != previsionId
                && await _acces.PerimetrePrevisionAsync(ctx, acteurId, existant.PrevisionRecetteId) is { } refusOrigine)
                return (false, refusOrigine);
            encaissement = existant;
            avant = new { encaissement.PrevisionRecetteId, encaissement.DateEncaissement, encaissement.Montant, encaissement.EstPositionne };
        }

        encaissement.PrevisionRecetteId = previsionId;
        encaissement.PeriodeId = periodeId.Value;
        encaissement.DateEncaissement = date;
        encaissement.Montant = s.Montant;
        encaissement.CompteBancaireId = s.CompteBancaireId;
        encaissement.EstPositionne = s.CompteBancaireId is not null;
        encaissement.Reference = reference;
        encaissement.Statut = StatutSaisie.Valide;
        encaissement.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesRecettes.ModuleAudit,
            nameof(Encaissement), encaissement.Id.ToString(), avant,
            new { encaissement.PrevisionRecetteId, encaissement.DateEncaissement, encaissement.Montant, encaissement.EstPositionne });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid encaissementId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.EncaissementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesRecettes.MessageDroit("supprimer un encaissement"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var encaissement = await ctx.Encaissements.Include(e => e.Periode).FirstOrDefaultAsync(e => e.Id == encaissementId);
        if (encaissement is null) return (false, "Encaissement introuvable.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, encaissement.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        if (encaissement.Periode.EstCloturee) return (false, "Le mois de cet encaissement est clos : il ne se supprime plus.");
        var refus = await _acces.PerimetrePrevisionAsync(ctx, acteurId, encaissement.PrevisionRecetteId);
        if (refus is not null) return (false, refus);

        encaissement.EstSupprime = true;
        encaissement.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesRecettes.ModuleAudit, nameof(Encaissement),
            encaissement.Id.ToString(), new { encaissement.PrevisionRecetteId, encaissement.DateEncaissement, encaissement.Montant }, null);
        return (true, null);
    }
}
