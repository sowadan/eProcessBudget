using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>Critères de l'historique des paiements (§ 3.4.2 : par ligne, projet ou entité).</summary>
public sealed class FiltrePaiements
{
    public Ulid ExerciceId { get; set; }
    public DateOnly? Du { get; set; }
    public DateOnly? Au { get; set; }
    public Ulid? LigneBudgetaireId { get; set; }
    public Ulid? ProjetId { get; set; }

    /// <summary>Bénéficiaire de l'engagement, ou entité de la dotation (appuis).</summary>
    public Ulid? TiersId { get; set; }

    public bool InclureAnnules { get; set; }

    /// <summary>Numéro de pièce, numéro d'engagement, objet.</summary>
    public string? Recherche { get; set; }
}

public sealed record LignePaiement(
    Ulid Id, string NumeroPiece, DateOnly Date, decimal Montant, ModePaiement Mode, StatutSaisie Statut,
    Ulid EngagementId, string Engagement, string Objet, string LigneCode, string? Beneficiaire, string? Facture, string? Compte);

public sealed class SaisiePaiement
{
    public Ulid EngagementId { get; set; }
    public Ulid? FactureId { get; set; }
    public string NumeroPiece { get; set; } = "";
    public DateOnly? Date { get; set; }
    public decimal Montant { get; set; }
    public ModePaiement Mode { get; set; } = ModePaiement.Virement;
    public Ulid? CompteBancaireId { get; set; }
}

public sealed class SaisieFacture
{
    public Ulid EngagementId { get; set; }
    public string Numero { get; set; } = "";
    public DateOnly? DateFacture { get; set; }
    public DateOnly? DateReception { get; set; }
    public decimal Montant { get; set; }
}

/// <summary>
/// Paiements et factures (cahier fonctionnel § 3.4.2, 4.2 et 4.3 ; technique § 4.4).
///
/// Le comptable (PaiementSaisir) enregistre les paiements d'un engagement validé : la
/// date tombe dans l'exercice ouvert (mois non clos), le montant ne dépasse pas le reste à
/// payer (montant engagé − paiements − rejets ou pénalités). Un paiement enregistré est
/// définitif ; une erreur se corrige par annulation motivée.
///
/// Réponse du métier C.2.2 : « la dépense, c'est le décaissement ; tout décaissement se
/// rapporte à une facture reçue ». Hors axe Appuis (versements sur convention), un paiement
/// porte donc sur une facture de l'engagement (ou de l'engagement d'origine d'un encours
/// reporté) et n'en dépasse pas le reste à payer. Les factures reçues forment la « prise en
/// charge » du compte sur chiffres.
/// </summary>
public interface IPaiementService
{
    Task<(List<LignePaiement> Items, int Total, decimal Montant)> ListerAsync(FiltrePaiements filtre, int page = 1, int taille = 20);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisiePaiement saisie);
    Task<(bool Ok, string? Erreur)> AnnulerAsync(Ulid paiementId, string motif);

    Task<(bool Ok, string? Erreur)> AjouterFactureAsync(SaisieFacture saisie);
    Task<(bool Ok, string? Erreur)> SupprimerFactureAsync(Ulid factureId);

    /// <summary>Factures non encore payées d'un engagement.</summary>
    Task<List<ElementListe>> ListerFacturesAsync(Ulid engagementId);

    Task<List<ElementListe>> ListerComptesAsync();
}

public sealed class PaiementService : IPaiementService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesExecution _acces;
    private readonly IAuditService _audit;

    public PaiementService(IDbContextFactory<AppDbContext> factory, AccesExecution acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<(List<LignePaiement> Items, int Total, decimal Montant)> ListerAsync(FiltrePaiements f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return ([], 0, 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Paiements.AsNoTracking().Where(p => p.ExerciceId == f.ExerciceId);
        if (!f.InclureAnnules) q = q.Where(p => p.Statut == StatutSaisie.Valide);
        if (f.Du is { } du) q = q.Where(p => p.DatePaiement >= du);
        if (f.Au is { } au) q = q.Where(p => p.DatePaiement <= au);
        if (f.LigneBudgetaireId is { } ligne) q = q.Where(p => p.Engagement.Dotation.LigneBudgetaireId == ligne);
        if (f.ProjetId is { } projet) q = q.Where(p => p.Engagement.Dotation.ProjetId == projet);
        if (f.TiersId is { } tiers) q = q.Where(p => p.Engagement.TiersId == tiers || p.Engagement.Dotation.TiersId == tiers);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(p => p.NumeroPiece.Contains(r) || p.Engagement.Numero.Contains(r) || p.Engagement.Objet.Contains(r));
        }

        var total = await q.CountAsync();
        var montant = await q.Where(p => p.Statut == StatutSaisie.Valide).SumAsync(p => (decimal?)p.Montant) ?? 0;
        var items = await ProjeterPaiements(q.OrderByDescending(p => p.DatePaiement).ThenByDescending(p => p.NumeroPiece)
            .Skip((page - 1) * taille).Take(taille)).ToListAsync();
        return (items, total, montant);
    }

    internal static IQueryable<LignePaiement> ProjeterPaiements(IQueryable<Paiement> q)
        => q.Select(p => new LignePaiement(p.Id, p.NumeroPiece, p.DatePaiement, p.Montant, p.ModePaiement, p.Statut,
            p.EngagementId, p.Engagement.Numero, p.Engagement.Objet, p.Engagement.Dotation.LigneBudgetaire.Code,
            p.Engagement.Tiers != null ? p.Engagement.Tiers.RaisonSociale : null,
            p.Facture != null ? p.Facture.Numero : null,
            p.CompteBancaire != null ? p.CompteBancaire.Banque + " " + p.CompteBancaire.NumeroCompte : null));

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisiePaiement s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PaiementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("enregistrer des paiements"));

        var numero = s.NumeroPiece?.Trim() ?? "";
        if (numero.Length == 0) return (false, "Le numéro de pièce est obligatoire.");
        if (numero.Length > 50) return (false, "Numéro de pièce : 50 caractères au plus.");
        if (s.Date is not { } date) return (false, "Indiquez la date du paiement.");
        if (s.Montant <= 0 || !CalculsExecution.EstMontantValide(s.Montant)) return (false, "Le montant est positif, à 2 décimales au plus.");
        if (!Enum.IsDefined(s.Mode)) return (false, "Mode de paiement inconnu.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.EngagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut != StatutSaisie.Valide) return (false, "On ne paie qu'un engagement validé.");

        // Règle § 4.3 : la date du paiement tombe dans l'exercice actif.
        var (exercice, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, e.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var erreurDate = await AccesExecution.DateSaisissableAsync(ctx, exercice!, date, "du paiement");
        if (erreurDate is not null) return (false, erreurDate);
        if (date < e.DateEngagement && !e.EstReport) return (false, "Un paiement ne précède pas l'engagement qu'il solde.");

        var codeAxe = await ctx.Dotations.Where(d => d.Id == e.DotationId).Select(d => d.LigneBudgetaire.SousRubrique.Rubrique.Axe.Code).FirstOrDefaultAsync();
        if (s.FactureId is { } factureId)
        {
            var engagements = await ChaineEngagementsAsync(ctx, e);
            var facture = await ctx.Factures.AsNoTracking().Where(f => f.Id == factureId && engagements.Contains(f.EngagementId))
                .Select(f => new { f.Montant, Paye = ctx.Paiements.Where(p => p.FactureId == f.Id && p.Statut == StatutSaisie.Valide).Sum(p => (decimal?)p.Montant) ?? 0 })
                .FirstOrDefaultAsync();
            if (facture is null) return (false, "Cette facture ne relève pas de l'engagement.");
            if (s.Montant > facture.Montant - facture.Paye)
                return (false, $"Le paiement ({s.Montant:N2}) dépasse le reste à payer de la facture ({facture.Montant - facture.Paye:N2}).");
        }
        else if (codeAxe != AxesDepense.Appuis.Code)
            return (false, "Tout décaissement se rapporte à une facture reçue : choisissez la facture payée (enregistrez-la d'abord sur l'engagement).");
        if (s.CompteBancaireId is { } compte && !await ctx.ComptesBancaires.AnyAsync(c => c.Id == compte && c.EstActif))
            return (false, "Compte bancaire introuvable ou inactif.");
        if (await ctx.Paiements.AnyAsync(p => p.NumeroPiece == numero)) return (false, $"Le numéro de pièce {numero} est déjà utilisé.");

        var refus = await _acces.PerimetreAsync(ctx, acteurId, e.DotationId);
        if (refus is not null) return (false, refus);

        // Règle § 4.2 : les paiements ne dépassent pas l'autorisation de dépense.
        var reste = await EngagementService.ResteAPayerAsync(ctx, e);
        if (s.Montant > reste) return (false, $"Le paiement ({s.Montant:N2}) dépasse le reste à payer de l'engagement ({reste:N2}).");

        var paiement = new Paiement
        {
            ExerciceId = e.ExerciceId, EngagementId = e.Id, FactureId = s.FactureId, NumeroPiece = numero, DatePaiement = date,
            Montant = s.Montant, ModePaiement = s.Mode, CompteBancaireId = s.CompteBancaireId, Statut = StatutSaisie.Valide, CreePar = acteurId
        };
        ctx.Paiements.Add(paiement);
        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (AccesExecution.EstConflitUnicite(ex))
        {
            return (false, $"Le numéro de pièce {numero} est déjà utilisé.");
        }

        await _audit.LoggerAsync(acteurId, null, "CREATION", AccesExecution.ModuleAudit, nameof(Paiement), paiement.Id.ToString(),
            null, new { paiement.NumeroPiece, Engagement = e.Numero, paiement.Montant, paiement.DatePaiement });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> AnnulerAsync(Ulid paiementId, string motif)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PaiementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("annuler un paiement"));
        motif = motif?.Trim() ?? "";
        if (motif.Length == 0) return (false, "Indiquez le motif de l'annulation.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var p = await ctx.Paiements.Include(x => x.Engagement).FirstOrDefaultAsync(x => x.Id == paiementId);
        if (p is null) return (false, "Paiement introuvable.");
        if (p.Statut != StatutSaisie.Valide) return (false, "Ce paiement est déjà annulé.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, p.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var refus = await _acces.PerimetreAsync(ctx, acteurId, p.Engagement.DotationId);
        if (refus is not null) return (false, refus);

        p.Statut = StatutSaisie.Annule;
        p.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "ANNULATION", AccesExecution.ModuleAudit, nameof(Paiement), p.Id.ToString(),
            null, new { p.NumeroPiece, p.Montant }, commentaire: motif);
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> AjouterFactureAsync(SaisieFacture s)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PaiementSaisir, TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("enregistrer des factures"));

        var numero = s.Numero?.Trim() ?? "";
        if (numero.Length == 0) return (false, "Le numéro de facture est obligatoire.");
        if (numero.Length > 50) return (false, "Numéro de facture : 50 caractères au plus.");
        if (s.DateFacture is not { } dateFacture || s.DateReception is not { } dateReception) return (false, "Indiquez les dates de facture et de réception.");
        if (dateReception < dateFacture) return (false, "La facture ne peut pas être reçue avant d'être émise.");
        if (s.Montant <= 0 || !CalculsExecution.EstMontantValide(s.Montant)) return (false, "Le montant est positif, à 2 décimales au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == s.EngagementId);
        if (e is null) return (false, "Engagement introuvable.");
        if (e.Statut != StatutSaisie.Valide) return (false, "Une facture se rattache à un engagement validé.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, e.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        if (await ctx.Factures.AnyAsync(f => f.EngagementId == e.Id && f.Numero == numero)) return (false, "Cette facture est déjà enregistrée sur l'engagement.");

        var facture = await ctx.Factures.Where(f => f.EngagementId == e.Id).SumAsync(f => (decimal?)f.Montant) ?? 0;
        if (facture + s.Montant > e.Montant)
            return (false, $"Le total facturé ({facture + s.Montant:N2}) dépasserait le montant engagé ({e.Montant:N2}).");

        var refus = await _acces.PerimetreAsync(ctx, acteurId, e.DotationId);
        if (refus is not null) return (false, refus);

        var f = new Facture
        {
            EngagementId = e.Id, Numero = numero, DateFacture = dateFacture, DateReception = dateReception,
            Montant = s.Montant, Statut = StatutSaisie.Valide, CreePar = acteurId
        };
        ctx.Factures.Add(f);
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "CREATION", AccesExecution.ModuleAudit, nameof(Facture), f.Id.ToString(),
            null, new { f.Numero, Engagement = e.Numero, f.Montant });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerFactureAsync(Ulid factureId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.PaiementSaisir, TypeDroit.EngagementSaisir);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("supprimer une facture"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var f = await ctx.Factures.Include(x => x.Engagement).FirstOrDefaultAsync(x => x.Id == factureId);
        if (f is null) return (false, "Facture introuvable.");
        if (await ctx.Paiements.AnyAsync(p => p.FactureId == factureId)) return (false, "Cette facture est rattachée à un paiement.");
        var (_, erreurExercice) = await AccesExecution.ExerciceOuvertAsync(ctx, f.Engagement.ExerciceId);
        if (erreurExercice is not null) return (false, erreurExercice);
        var refus = await _acces.PerimetreAsync(ctx, acteurId, f.Engagement.DotationId);
        if (refus is not null) return (false, refus);

        f.EstSupprime = true;
        f.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesExecution.ModuleAudit, nameof(Facture), f.Id.ToString(),
            new { f.Numero, f.Montant }, null);
        return (true, null);
    }

    public async Task<List<ElementListe>> ListerFacturesAsync(Ulid engagementId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var e = await ctx.Engagements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == engagementId);
        if (e is null) return [];
        var engagements = await ChaineEngagementsAsync(ctx, e);
        var factures = await ctx.Factures.AsNoTracking()
            .Where(f => engagements.Contains(f.EngagementId))
            .OrderBy(f => f.DateFacture)
            .Select(f => new { f.Id, f.Numero, f.DateFacture, f.Montant, Paye = ctx.Paiements.Where(p => p.FactureId == f.Id && p.Statut == StatutSaisie.Valide).Sum(p => (decimal?)p.Montant) ?? 0 })
            .ToListAsync();
        return factures.Where(f => f.Montant > f.Paye)
            .Select(f => new ElementListe(f.Id, f.Paye == 0
                ? $"{f.Numero} du {f.DateFacture:dd/MM/yyyy} — {f.Montant:N2}"
                : $"{f.Numero} du {f.DateFacture:dd/MM/yyyy} — reste {f.Montant - f.Paye:N2} sur {f.Montant:N2}"))
            .ToList();
    }

    /// <summary>
    /// L'engagement et ses engagements d'origine (un encours reporté remonte à l'engagement
    /// de l'exercice où la facture a pu être reçue).
    /// </summary>
    internal static async Task<List<Ulid>> ChaineEngagementsAsync(AppDbContext ctx, Engagement e)
    {
        var chaine = new List<Ulid> { e.Id };
        var origine = e.EngagementOrigineId;
        while (origine is { } id && !chaine.Contains(id) && chaine.Count < 50)
        {
            chaine.Add(id);
            origine = await ctx.Engagements.AsNoTracking().Where(x => x.Id == id).Select(x => x.EngagementOrigineId).FirstOrDefaultAsync();
        }
        return chaine;
    }

    public async Task<List<ElementListe>> ListerComptesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ComptesBancaires.AsNoTracking().Where(c => c.EstActif).OrderBy(c => c.Banque)
            .Select(c => new ElementListe(c.Id, c.Banque + " — " + c.NumeroCompte)).ToListAsync();
    }
}
