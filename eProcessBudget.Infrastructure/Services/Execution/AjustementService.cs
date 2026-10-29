using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>Critères des listes de pièces (ajustements, engagements) d'un exercice.</summary>
public sealed class FiltrePieces
{
    public Ulid ExerciceId { get; set; }
    public StatutSaisie? Statut { get; set; }
    public TypeEngagement? Type { get; set; }
    public Ulid? DotationId { get; set; }
    public Ulid? TiersId { get; set; }

    /// <summary>Numéro, objet ou code de ligne.</summary>
    public string? Recherche { get; set; }
}

/// <summary>Saisie d'un ajustement : montant signé (+ hausse, − baisse), motif et justification obligatoires.</summary>
public sealed class SaisieAjustement
{
    public Ulid Id { get; set; }
    public Ulid? DotationId { get; set; }
    public DateOnly? Date { get; set; }
    public decimal Montant { get; set; }
    public Ulid? MotifId { get; set; }
    public string Justification { get; set; } = "";
}

/// <summary>
/// Ajustements budgétaires (cahier fonctionnel § 3.4.2 et 4.2) : hausse ou baisse d'une
/// dotation, motif obligatoire. La saisie vaut soumission ; le Responsable Budget valide
/// (AjustementValider) ou rejette. Seul un ajustement validé modifie la prévision ajustée.
/// </summary>
public interface IAjustementService
{
    Task<(List<LigneAjustement> Items, int Total)> ListerAsync(FiltrePieces filtre, int page = 1, int taille = 20);
    Task<SaisieAjustement?> ObtenirSaisieAsync(Ulid ajustementId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieAjustement saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid ajustementId);
    Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid ajustementId);
    Task<(bool Ok, string? Erreur)> RejeterAsync(Ulid ajustementId, string motif);
}

public sealed class AjustementService : IAjustementService
{
    private const string Prefixe = "AJ";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesExecution _acces;
    private readonly IAuditService _audit;
    private readonly IAlerteService _alertes;
    private readonly IDroitService _droits;
    private readonly INotificationService _notifications;

    public AjustementService(IDbContextFactory<AppDbContext> factory, AccesExecution acces, IAuditService audit,
                             IAlerteService alertes, IDroitService droits, INotificationService notifications)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
        _alertes = alertes;
        _droits = droits;
        _notifications = notifications;
    }

    public async Task<(List<LigneAjustement> Items, int Total)> ListerAsync(FiltrePieces f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Ajustements.AsNoTracking().Where(a => a.Dotation.ExerciceId == f.ExerciceId);
        if (f.Statut is { } statut) q = q.Where(a => a.Statut == statut);
        if (f.DotationId is { } dotation) q = q.Where(a => a.DotationId == dotation);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(a => a.Numero.Contains(r) || a.Dotation.LigneBudgetaire.Code.Contains(r) || a.Justification.Contains(r));
        }
        var total = await q.CountAsync();
        var items = await SituationService.ProjeterAjustements(q.OrderByDescending(a => a.DateAjustement).ThenByDescending(a => a.Numero)
            .Skip((page - 1) * taille).Take(taille)).ToListAsync();
        return (items, total);
    }

    public async Task<SaisieAjustement?> ObtenirSaisieAsync(Ulid ajustementId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Ajustements.AsNoTracking().Where(a => a.Id == ajustementId)
            .Select(a => new SaisieAjustement
            {
                Id = a.Id, DotationId = a.DotationId, Date = a.DateAjustement, Montant = a.Montant,
                MotifId = a.MotifAjustementId, Justification = a.Justification
            })
            .FirstOrDefaultAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieAjustement s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.AjustementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("saisir des ajustements"));

        if (s.DotationId is not { } dotationId) return (false, "Choisissez la dotation ajustée.");
        if (s.Date is not { } date) return (false, "Indiquez la date de l'ajustement.");
        if (s.Montant == 0 || !CalculsExecution.EstMontantValide(s.Montant))
            return (false, "Le montant est non nul (positif pour une hausse, négatif pour une baisse), à 2 décimales au plus.");
        if (s.MotifId is not { } motifId) return (false, "Le motif est obligatoire.");
        var justification = s.Justification?.Trim() ?? "";
        if (justification.Length == 0) return (false, "La justification est obligatoire.");
        if (justification.Length > 1000) return (false, "Justification : 1 000 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var dotation = await ctx.Dotations.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dotationId);
        if (dotation is null) return (false, "Dotation introuvable.");
        if (dotation.Statut != StatutSaisie.Valide)
            return (false, "L'exercice de cette dotation n'est pas encore ouvert : modifiez directement sa prévision initiale.");

        var (exercice, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, dotation.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var erreurDate = await AccesExecution.DateSaisissableAsync(ctx, exercice!, date, "de l'ajustement");
        if (erreurDate is not null) return (false, erreurDate);

        var sensMotif = await ctx.MotifsAjustement.Where(m => m.Id == motifId && m.EstActif).Select(m => (SensAjustement?)m.Sens).FirstOrDefaultAsync();
        if (sensMotif is null) return (false, "Motif introuvable ou inactif.");
        if ((sensMotif == SensAjustement.Hausse && s.Montant < 0) || (sensMotif == SensAjustement.Baisse && s.Montant > 0))
            return (false, $"Ce motif ne s'applique qu'à une {(sensMotif == SensAjustement.Hausse ? "hausse" : "baisse")}.");

        var refus = await _acces.PerimetreAsync(ctx, acteurId, dotationId);
        if (refus is not null) return (false, refus);

        var erreurBaisse = await ControlerBaisseAsync(ctx, dotationId, s.Montant);
        if (erreurBaisse is not null) return (false, erreurBaisse);

        Ajustement ajustement;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            ajustement = new Ajustement { CreePar = acteurId };
            ctx.Ajustements.Add(ajustement);
        }
        else
        {
            var existant = await ctx.Ajustements.FirstOrDefaultAsync(a => a.Id == s.Id);
            if (existant is null) return (false, "Ajustement introuvable.");
            if (existant.Statut is not (StatutSaisie.Soumis or StatutSaisie.Rejete)) return (false, "Un ajustement validé n'est plus modifiable.");
            // Changer de dotation exige aussi le périmètre de celle qu'il quitte.
            if (existant.DotationId != dotationId && await _acces.PerimetreAsync(ctx, acteurId, existant.DotationId) is { } refusOrigine)
                return (false, refusOrigine);
            ajustement = existant;
            avant = new { ajustement.DotationId, ajustement.Montant, ajustement.Statut };
        }

        ajustement.DotationId = dotationId;
        ajustement.DateAjustement = date;
        ajustement.Montant = s.Montant;
        ajustement.MotifAjustementId = motifId;
        ajustement.Justification = justification;
        ajustement.Statut = StatutSaisie.Soumis;
        ajustement.ModifiePar = acteurId;

        var erreurEnregistrement = await EnregistrerNumeroteAsync(ctx, ajustement, exercice!.Annee);
        if (erreurEnregistrement is not null) return (false, erreurEnregistrement);

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesExecution.ModuleAudit,
            nameof(Ajustement), ajustement.Id.ToString(), avant, new { ajustement.Numero, ajustement.DotationId, ajustement.Montant });

        var valideurs = await DetenteursDroit.ListerAsync(ctx, _droits, ctx.OrganismeCourantId, TypeDroit.AjustementValider);
        await _notifications.NotifierGroupeAsync(TypeNotification.ActionRequise,
            $"L'ajustement {ajustement.Numero} ({(ajustement.Montant > 0 ? "hausse" : "baisse")} de {Math.Abs(ajustement.Montant):N2}) attend votre validation.",
            valideurs.Where(v => v != acteurId), titre: "Ajustement à valider", lien: "/execution/ajustements",
            cleEntite: $"Ajustement:{ajustement.Id}", acteurId: acteurId);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid ajustementId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.AjustementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("supprimer un ajustement"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var a = await ctx.Ajustements.FirstOrDefaultAsync(x => x.Id == ajustementId);
        if (a is null) return (false, "Ajustement introuvable.");
        if (a.Statut is not (StatutSaisie.Soumis or StatutSaisie.Rejete)) return (false, "Un ajustement validé ne se supprime pas : saisissez un ajustement inverse.");
        var refus = await _acces.PerimetreAsync(ctx, acteurId, a.DotationId);
        if (refus is not null) return (false, refus);

        a.EstSupprime = true;
        a.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesExecution.ModuleAudit, nameof(Ajustement), a.Id.ToString(),
            new { a.Numero, a.Montant }, null);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid ajustementId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.AjustementValider);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("valider les ajustements"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var a = await ctx.Ajustements.Include(x => x.Dotation).FirstOrDefaultAsync(x => x.Id == ajustementId);
        if (a is null) return (false, "Ajustement introuvable.");
        if (a.Statut != StatutSaisie.Soumis) return (false, "Seul un ajustement soumis se valide.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, a.Dotation.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);

        // La situation a pu changer depuis la saisie (engagements validés entre-temps).
        var erreurBaisse = await ControlerBaisseAsync(ctx, a.DotationId, a.Montant);
        if (erreurBaisse is not null) return (false, erreurBaisse);

        a.Statut = StatutSaisie.Valide;
        a.ValideParId = acteurId;
        a.DateValidation = DateTime.UtcNow;
        a.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "VALIDATION", AccesExecution.ModuleAudit, nameof(Ajustement), a.Id.ToString(),
            null, new { a.Numero, a.DotationId, a.Montant });
        await _notifications.NotifierAsync(TypeNotification.Succes, $"L'ajustement {a.Numero} est validé.", a.CreePar ?? Ulid.Empty,
            titre: "Ajustement validé", lien: $"/execution/dotations/{a.DotationId}", acteurId: acteurId);
        if (a.Montant < 0) await _alertes.VerifierAsync([a.DotationId]);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> RejeterAsync(Ulid ajustementId, string motif)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.AjustementValider);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("rejeter les ajustements"));
        motif = motif?.Trim() ?? "";
        if (motif.Length == 0) return (false, "Indiquez le motif du rejet.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var a = await ctx.Ajustements.FirstOrDefaultAsync(x => x.Id == ajustementId);
        if (a is null) return (false, "Ajustement introuvable.");
        if (a.Statut != StatutSaisie.Soumis) return (false, "Seul un ajustement soumis se rejette.");

        a.Statut = StatutSaisie.Rejete;
        a.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "REJET", AccesExecution.ModuleAudit, nameof(Ajustement), a.Id.ToString(),
            null, new { a.Numero, a.Montant }, commentaire: motif);
        await _notifications.NotifierAsync(TypeNotification.Avertissement, $"L'ajustement {a.Numero} est rejeté : {motif}", a.CreePar ?? Ulid.Empty,
            titre: "Ajustement rejeté", lien: "/execution/ajustements", acteurId: acteurId);
        return (true, null);
    }

    /// <summary>
    /// Une baisse ne ramène ni la prévision ajustée sous zéro, ni la dotation totale sous
    /// les engagements déjà validés.
    /// </summary>
    private static async Task<string?> ControlerBaisseAsync(AppDbContext ctx, Ulid dotationId, decimal montant)
    {
        if (montant >= 0) return null;
        var m = await SituationService.MontantsAsync(ctx, dotationId) ?? MontantsDotation.Zero;
        if (m.PrevisionAjustee + montant < 0)
            return $"La baisse ramènerait la prévision ajustée ({m.PrevisionAjustee:N2}) sous zéro.";
        if (m.DotationTotale + montant < m.AETotal)
            return $"La baisse ramènerait la dotation totale ({m.DotationTotale + montant:N2}) sous les engagements validés ({m.AETotal:N2}).";
        return null;
    }

    /// <summary>Attribue le numéro (AJ-année-rang) d'un nouvel ajustement et enregistre ; une collision de numéro est retentée.</summary>
    private static async Task<string?> EnregistrerNumeroteAsync(AppDbContext ctx, Ajustement a, int annee)
    {
        var nouveau = string.IsNullOrEmpty(a.Numero);
        for (var essai = 0; ; essai++)
        {
            if (nouveau)
                a.Numero = await AccesExecution.NumeroSuivantAsync(
                    ctx.Ajustements.IgnoreQueryFilters().Where(x => x.OrganismeId == ctx.OrganismeCourantId).Select(x => x.Numero), Prefixe, annee);
            try
            {
                await ctx.SaveChangesAsync();
                return null;
            }
            catch (DbUpdateException ex) when (nouveau && essai < 3 && AccesExecution.EstConflitUnicite(ex))
            {
                // Un autre utilisateur a pris ce numéro entre-temps : on en prend un autre.
            }
        }
    }
}
