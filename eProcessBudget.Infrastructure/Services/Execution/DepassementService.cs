using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>Une demande de validation de dépassement, avec ce qu'il faut pour décider.</summary>
public sealed record LigneDepassement(
    Ulid Id, Ulid EngagementId, string Engagement, string Objet, decimal MontantEngage, string LigneCode, string LigneLibelle,
    Ulid DotationId, decimal MontantDepassement, decimal TauxApres, string Motif, string? Demandeur, DateTime DateDemande,
    StatutValidation Statut, string? Decideur, DateTime? DateDecision, string? Commentaire);

/// <summary>
/// File des dépassements (cahier fonctionnel § 4.2) : un dépassement de dotation n'est
/// pas bloquant, mais l'engagement attend la décision du Responsable Budget
/// (DepassementValider). Accepter valide l'engagement ; refuser le rejette. Auteur, date et
/// motif de la décision sont journalisés.
/// </summary>
public interface IDepassementService
{
    Task<(List<LigneDepassement> Items, int Total)> ListerAsync(StatutValidation? statut, int page = 1, int taille = 20);
    Task<int> CompterEnAttenteAsync();
    Task<(bool Ok, string? Erreur)> DeciderAsync(Ulid depassementId, bool accepter, string? commentaire);
}

public sealed class DepassementService : IDepassementService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesExecution _acces;
    private readonly IAuditService _audit;
    private readonly IAlerteService _alertes;
    private readonly INotificationService _notifications;

    public DepassementService(IDbContextFactory<AppDbContext> factory, AccesExecution acces, IAuditService audit,
                              IAlerteService alertes, INotificationService notifications)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
        _alertes = alertes;
        _notifications = notifications;
    }

    public async Task<(List<LigneDepassement> Items, int Total)> ListerAsync(StatutValidation? statut, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.ValidationsDepassement.AsNoTracking();
        if (statut is { } s) q = q.Where(v => v.Statut == s);
        var total = await q.CountAsync();
        var items = await q.OrderBy(v => v.Statut).ThenByDescending(v => v.DateCreation)
            .Skip((page - 1) * taille).Take(taille)
            .Select(v => new LigneDepassement(v.Id, v.EngagementId, v.Engagement.Numero, v.Engagement.Objet, v.Engagement.Montant,
                v.Dotation.LigneBudgetaire.Code, v.Dotation.LigneBudgetaire.Libelle, v.DotationId,
                v.MontantDepassement, v.TauxApres, v.Motif,
                ctx.Utilisateurs.Where(u => u.Id == v.CreePar).Select(u => u.NomPrenoms).FirstOrDefault(),
                v.DateCreation, v.Statut,
                ctx.Utilisateurs.Where(u => u.Id == v.DecideParId).Select(u => u.NomPrenoms).FirstOrDefault(),
                v.DateDecision, v.CommentaireDecision))
            .ToListAsync();
        return (items, total);
    }

    public async Task<int> CompterEnAttenteAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return 0;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ValidationsDepassement.CountAsync(v => v.Statut == StatutValidation.EnAttente);
    }

    public async Task<(bool Ok, string? Erreur)> DeciderAsync(Ulid depassementId, bool accepter, string? commentaire)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.DepassementValider);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("décider des dépassements"));
        commentaire = string.IsNullOrWhiteSpace(commentaire) ? null : commentaire.Trim();
        if (!accepter && commentaire is null) return (false, "Indiquez le motif du refus.");
        if (commentaire?.Length > 1000) return (false, "Commentaire : 1 000 caractères au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var v = await ctx.ValidationsDepassement.Include(x => x.Engagement).FirstOrDefaultAsync(x => x.Id == depassementId);
        if (v is null) return (false, "Demande introuvable.");
        if (v.Statut != StatutValidation.EnAttente) return (false, "Cette demande a déjà été tranchée.");
        var e = v.Engagement;
        if (e.Statut != StatutSaisie.Soumis) return (false, "L'engagement n'est plus en attente de validation.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, e.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);

        v.Statut = accepter ? StatutValidation.Validee : StatutValidation.Rejetee;
        v.DecideParId = acteurId;
        v.DateDecision = DateTime.UtcNow;
        v.CommentaireDecision = commentaire;
        v.ModifiePar = acteurId;

        if (accepter)
        {
            // La décision du Responsable Budget vaut validation de l'engagement.
            e.Statut = StatutSaisie.Valide;
            e.ValideParId = acteurId;
            e.DateValidation = DateTime.UtcNow;
            e.ModifiePar = acteurId;
            await ctx.SaveChangesAsync();
        }
        else
        {
            await EngagementService.TerminerAsync(ctx, e, StatutSaisie.Rejete, acteurId, commentaire!);
        }

        await _audit.LoggerAsync(acteurId, null, accepter ? "VALIDATION_DEPASSEMENT" : "REJET_DEPASSEMENT", AccesExecution.ModuleAudit,
            nameof(ValidationDepassement), v.Id.ToString(), null,
            new { Engagement = e.Numero, v.MontantDepassement, v.TauxApres, Decision = v.Statut.ToString() }, commentaire: commentaire);
        await _notifications.NotifierAsync(accepter ? TypeNotification.Succes : TypeNotification.Avertissement,
            accepter
                ? $"Le dépassement de l'engagement {e.Numero} est accepté : l'engagement est validé."
                : $"Le dépassement de l'engagement {e.Numero} est refusé : {commentaire}",
            e.CreePar ?? Ulid.Empty, titre: accepter ? "Dépassement accepté" : "Dépassement refusé",
            lien: $"/execution/engagements/{e.Id}", acteurId: acteurId);
        if (accepter) await _alertes.VerifierAsync([e.DotationId]);
        return (true, null);
    }
}
