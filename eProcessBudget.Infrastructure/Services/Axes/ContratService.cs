using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Axes;

/// <summary>Critères de la liste des contrats d'un axe.</summary>
public sealed class FiltreContrats
{
    public Ulid? ProjetId { get; set; }
    public TypeContrat? Type { get; set; }
    public Ulid? TitulaireId { get; set; }
    public StatutProjet? Statut { get; set; }

    /// <summary>Numéro ou objet.</summary>
    public string? Recherche { get; set; }
}

/// <summary>Un contrat et son exécution (engagé, payé, kilomètres), tous exercices confondus.</summary>
public sealed record LigneContrat(
    Ulid Id, string Numero, string Objet, TypeContrat Type, string? TypePrestation, Ulid ProjetId, string Projet,
    string? ContratTravaux, string Titulaire, decimal MontantInitial, decimal MontantAvenants,
    DateOnly? DateSignature, DateOnly? DateDebut, DateOnly? DateFin, bool EstPluriannuel,
    decimal? KmContractuels, decimal KmRealises, StatutProjet Statut, decimal Engage, decimal Paye)
{
    /// <summary>Montant initial + avenants.</summary>
    public decimal Montant => CalculsAxes.MontantContrat(MontantInitial, MontantAvenants);

    public decimal? TauxEngagement => CalculsExecution.Taux(Engage, Montant);
    public decimal? TauxPaiement => CalculsExecution.Taux(Paye, Montant);
    public decimal? TauxKm => CalculsAxes.TauxAvancementKm(KmRealises, KmContractuels);

    /// <summary>Plus engagé que le montant du contrat (avenant manquant ?).</summary>
    public bool EngageAuDela => Engage > Montant;
}

public sealed class SaisieContrat
{
    public Ulid Id { get; set; }
    public Ulid? ProjetId { get; set; }
    public string Numero { get; set; } = "";
    public string Objet { get; set; } = "";
    public TypeContrat Type { get; set; } = TypeContrat.Travaux;
    public Ulid? TypePrestationId { get; set; }
    public Ulid? ContratTravauxId { get; set; }
    public Ulid? TitulaireId { get; set; }
    public decimal MontantInitial { get; set; }
    public decimal MontantAvenants { get; set; }
    public DateOnly? DateSignature { get; set; }
    public DateOnly? DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
    public bool EstPluriannuel { get; set; }
    public decimal? KmContractuels { get; set; }
    public StatutProjet Statut { get; set; } = StatutProjet.EnCours;
}

/// <summary>
/// Contrats et marchés des projets (cahier fonctionnel § 3.5.2 et 3.5.3 ; technique § 4.5).
///
/// Entretien routier : contrats de travaux (kilométrage contractuel) et contrats de
/// prestations (études, contrôle, surveillance, qualité), chaque prestation étant associée
/// à une opération de travaux et, le cas échéant, au contrat de travaux qu'elle contrôle.
/// Un contrat pluriannuel donne lieu à une AE sur sa durée et à des CP annuels : c'est
/// l'échéancier des CP de l'engagement (cycle commun). Appuis : conventions d'appui.
/// </summary>
public interface IContratService
{
    Task<(List<LigneContrat> Items, int Total)> ListerAsync(AxeDepense axe, FiltreContrats filtre, int page = 1, int taille = 20);
    Task<SaisieContrat?> ObtenirSaisieAsync(Ulid contratId);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieContrat saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid contratId);

    /// <summary>Contrats non annulés d'un projet (d'un type donné le cas échéant), pour un choix.</summary>
    Task<List<ElementListe>> ListerPourSelectionAsync(Ulid projetId, TypeContrat? type = null);
}

public sealed class ContratService : IContratService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesAxes _acces;
    private readonly IAuditService _audit;

    public ContratService(IDbContextFactory<AppDbContext> factory, AccesAxes acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<(List<LigneContrat> Items, int Total)> ListerAsync(AxeDepense axe, FiltreContrats f, int page = 1, int taille = 20)
    {
        if (await _acces.LecteurAsync(axe) == Ulid.Empty) return ([], 0);
        taille = Math.Clamp(taille, 1, 200);
        page = Math.Max(1, page);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Contrats.AsNoTracking().Where(c => c.Projet.Axe.Code == axe.Code);
        if (f.ProjetId is { } projet) q = q.Where(c => c.ProjetId == projet);
        if (f.Type is { } type) q = q.Where(c => c.TypeContrat == type);
        if (f.TitulaireId is { } titulaire) q = q.Where(c => c.TitulaireId == titulaire);
        if (f.Statut is { } statut) q = q.Where(c => c.Statut == statut);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(c => c.Numero.Contains(r) || c.Objet.Contains(r));
        }

        var total = await q.CountAsync();
        var lignes = await q.OrderBy(c => c.Projet.Code).ThenBy(c => c.TypeContrat).ThenBy(c => c.Numero)
            .Skip((page - 1) * taille).Take(taille)
            .Select(c => new
            {
                c.Id, c.Numero, c.Objet, c.TypeContrat,
                TypePrestation = c.TypePrestation != null ? c.TypePrestation.Libelle : null,
                c.ProjetId, Projet = c.Projet.Code,
                ContratTravaux = c.ContratTravaux != null ? c.ContratTravaux.Numero : null,
                Titulaire = c.Titulaire.RaisonSociale,
                c.MontantInitial, c.MontantAvenants, c.DateSignature, c.DateDebut, c.DateFin, c.EstPluriannuel, c.KmContractuels, c.Statut,
                KmRealises = ctx.AvancementsPhysiques.Where(a => a.ContratId == c.Id).Sum(a => a.KmRealises) ?? 0
            })
            .ToListAsync();

        var execution = await RequetesAxes.ExecutionParContratAsync(ctx, lignes.Select(l => l.Id).ToList());
        return (lignes.Select(l =>
        {
            var (engage, paye) = execution.GetValueOrDefault(l.Id);
            return new LigneContrat(l.Id, l.Numero, l.Objet, l.TypeContrat, l.TypePrestation, l.ProjetId, l.Projet, l.ContratTravaux,
                l.Titulaire, l.MontantInitial, l.MontantAvenants, l.DateSignature, l.DateDebut, l.DateFin, l.EstPluriannuel,
                l.KmContractuels, l.KmRealises, l.Statut, engage, paye);
        }).ToList(), total);
    }

    public async Task<SaisieContrat?> ObtenirSaisieAsync(Ulid contratId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var codeAxe = await ctx.Contrats.AsNoTracking().Where(c => c.Id == contratId).Select(c => c.Projet.Axe.Code).FirstOrDefaultAsync();
        if (AxesDepense.DeCode(codeAxe) is not { } axe || await _acces.LecteurAsync(axe) == Ulid.Empty) return null;
        return await ctx.Contrats.AsNoTracking().Where(c => c.Id == contratId)
            .Select(c => new SaisieContrat
            {
                Id = c.Id, ProjetId = c.ProjetId, Numero = c.Numero, Objet = c.Objet, Type = c.TypeContrat,
                TypePrestationId = c.TypePrestationId, ContratTravauxId = c.ContratTravauxId, TitulaireId = c.TitulaireId,
                MontantInitial = c.MontantInitial, MontantAvenants = c.MontantAvenants, DateSignature = c.DateSignature,
                DateDebut = c.DateDebut, DateFin = c.DateFin, EstPluriannuel = c.EstPluriannuel, KmContractuels = c.KmContractuels,
                Statut = c.Statut
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<ElementListe>> ListerPourSelectionAsync(Ulid projetId, TypeContrat? type = null)
    {
        if (await _acces.LecteurUnAxeAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Contrats.AsNoTracking().Where(c => c.ProjetId == projetId && c.Statut != StatutProjet.Annule);
        if (type is { } t) q = q.Where(c => c.TypeContrat == t);
        return await q.OrderBy(c => c.Numero).Select(c => new ElementListe(c.Id, c.Numero + " — " + c.Objet)).ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieContrat s)
    {
        if (s.ProjetId is not { } projetId) return (false, "Choisissez le projet ou l'opération du contrat.");
        var numero = s.Numero?.Trim() ?? "";
        var objet = s.Objet?.Trim() ?? "";
        if (numero.Length == 0) return (false, "Le numéro est obligatoire.");
        if (numero.Length > 50) return (false, "Numéro : 50 caractères au plus.");
        if (objet.Length == 0) return (false, "L'objet est obligatoire.");
        if (objet.Length > 500) return (false, "Objet : 500 caractères au plus.");
        if (!Enum.IsDefined(s.Type)) return (false, "Type de contrat inconnu.");
        if (!Enum.IsDefined(s.Statut)) return (false, "Statut inconnu.");
        if (s.TitulaireId is not { } titulaireId) return (false, "Indiquez le titulaire du contrat.");
        if (s.MontantInitial <= 0 || !CalculsExecution.EstMontantValide(s.MontantInitial))
            return (false, "Le montant initial est positif, à 2 décimales au plus.");
        if (s.MontantAvenants < 0 || !CalculsExecution.EstMontantValide(s.MontantAvenants))
            return (false, "Le total des avenants est positif ou nul, à 2 décimales au plus.");
        if (s.DateDebut is { } debut && s.DateFin is { } fin && fin < debut) return (false, "La date de fin précède la date de début.");
        if (s.KmContractuels is { } km && (km < 0 || decimal.Round(km, 4) != km))
            return (false, "Le kilométrage contractuel est positif, à 4 décimales au plus.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var projet = await ctx.Projets.AsNoTracking().Where(p => p.Id == projetId)
            .Select(p => new { p.AxeId, CodeAxe = p.Axe.Code, p.StructureId, p.SiteId, p.Statut }).FirstOrDefaultAsync();
        if (projet is null || AxesDepense.DeCode(projet.CodeAxe) is not { } axe) return (false, "Projet introuvable.");
        var acteurId = await _acces.SaisisseurAsync(axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(axe));
        if (projet.Statut == StatutProjet.Annule) return (false, "Le projet est annulé.");
        if (!LibellesAxes.TypesContrat(axe.Code).Contains(s.Type))
            return (false, $"Un contrat « {LibellesAxes.TypeContrat[s.Type]} » n'a pas sa place sur l'axe {axe.Libelle}.");

        // Prestation : type obligatoire ; contrat de travaux contrôlé facultatif, sur la même opération.
        if (s.Type == TypeContrat.Prestation)
        {
            if (s.TypePrestationId is not { } tp) return (false, "Indiquez le type de prestation (études, contrôle, surveillance, qualité).");
            if (!await ctx.ListesValeurs.AnyAsync(x => x.Id == tp && x.EstActif && x.Categorie == CategoriesListeValeur.TypePrestation))
                return (false, "Type de prestation introuvable ou inactif.");
            if (s.ContratTravauxId is { } ct
                && !await ctx.Contrats.AnyAsync(x => x.Id == ct && x.Id != s.Id && x.ProjetId == projetId && x.TypeContrat == TypeContrat.Travaux))
                return (false, "Le contrat contrôlé doit être un contrat de travaux de la même opération.");
        }
        var typeTitulaire = s.Type == TypeContrat.ConventionAppui ? TypeTiers.EntiteBeneficiaireAppuis : TypeTiers.Prestataire;
        if (!await ctx.Tiers.AnyAsync(x => x.Id == titulaireId && x.EstActif && x.TypeTiers == typeTitulaire))
            return (false, s.Type == TypeContrat.ConventionAppui
                ? "Le titulaire d'une convention d'appui est une entité bénéficiaire active."
                : "Le titulaire est un prestataire ou une entreprise actif.");

        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: projet.AxeId, StructureId: projet.StructureId, SiteId: projet.SiteId));
        if (refus is not null) return (false, refus);

        if (await ctx.Contrats.AnyAsync(c => c.Id != s.Id && c.Numero == numero))
            return (false, $"Le numéro {numero} est déjà utilisé par un autre contrat.");

        Contrat contrat;
        object? avant = null;
        if (s.Id == Ulid.Empty)
        {
            contrat = new Contrat { CreePar = acteurId };
            ctx.Contrats.Add(contrat);
        }
        else
        {
            var existant = await ctx.Contrats.FirstOrDefaultAsync(c => c.Id == s.Id);
            if (existant is null) return (false, "Contrat introuvable.");
            if (existant.ProjetId != projetId && await ctx.Engagements.AnyAsync(e => e.ContratId == existant.Id))
                return (false, "Ce contrat porte des engagements : il ne change plus de projet.");
            // Déplacer un contrat exige aussi le périmètre du projet qu'il quitte.
            if (existant.ProjetId != projetId && await _acces.PerimetreProjetAsync(ctx, acteurId, existant.ProjetId) is { } refusOrigine)
                return (false, refusOrigine);
            if (existant.TypeContrat == TypeContrat.Travaux && s.Type != TypeContrat.Travaux
                && await ctx.Contrats.AnyAsync(c => c.ContratTravauxId == existant.Id))
                return (false, "Des prestations contrôlent ce contrat de travaux : il reste un contrat de travaux.");
            contrat = existant;
            avant = new { contrat.Numero, contrat.MontantInitial, contrat.MontantAvenants, contrat.Statut };
        }

        contrat.ProjetId = projetId;
        contrat.Numero = numero;
        contrat.Objet = objet;
        contrat.TypeContrat = s.Type;
        contrat.TypePrestationId = s.Type == TypeContrat.Prestation ? s.TypePrestationId : null;
        contrat.ContratTravauxId = s.Type == TypeContrat.Prestation ? s.ContratTravauxId : null;
        contrat.TitulaireId = titulaireId;
        contrat.MontantInitial = s.MontantInitial;
        contrat.MontantAvenants = s.MontantAvenants;
        contrat.DateSignature = s.DateSignature;
        contrat.DateDebut = s.DateDebut;
        contrat.DateFin = s.DateFin;
        contrat.EstPluriannuel = s.EstPluriannuel;
        contrat.KmContractuels = s.Type == TypeContrat.Travaux ? s.KmContractuels : null;
        contrat.Statut = s.Statut;
        contrat.ModifiePar = acteurId;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (AccesExecution.EstConflitUnicite(ex))
        {
            return (false, $"Le numéro {numero} est déjà utilisé par un autre contrat.");
        }

        await _audit.LoggerAsync(acteurId, null, avant is null ? "CREATION" : "MODIFICATION", AccesAxes.ModuleAudit,
            nameof(Contrat), contrat.Id.ToString(), avant, new { contrat.Numero, contrat.MontantInitial, contrat.MontantAvenants, contrat.Statut });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid contratId)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var contrat = await ctx.Contrats.FirstOrDefaultAsync(c => c.Id == contratId);
        if (contrat is null) return (false, "Contrat introuvable.");
        var projet = await ctx.Projets.AsNoTracking().Where(p => p.Id == contrat.ProjetId)
            .Select(p => new { p.AxeId, CodeAxe = p.Axe.Code, p.StructureId, p.SiteId }).FirstAsync();
        if (AxesDepense.DeCode(projet.CodeAxe) is not { } axe) return (false, "Contrat introuvable.");
        var acteurId = await _acces.SaisisseurAsync(axe);
        if (acteurId == Ulid.Empty) return (false, AccesAxes.MessageSaisie(axe));
        var refus = await _acces.PerimetreAsync(acteurId, new CiblePerimetre(AxeId: projet.AxeId, StructureId: projet.StructureId, SiteId: projet.SiteId));
        if (refus is not null) return (false, refus);

        if (await ctx.Engagements.AnyAsync(e => e.ContratId == contratId))
            return (false, "Ce contrat porte des engagements : il ne peut pas être supprimé (passez-le au statut Achevé ou Annulé).");
        if (await ctx.Contrats.AnyAsync(c => c.ContratTravauxId == contratId))
            return (false, "Des prestations contrôlent ce contrat de travaux : il ne peut pas être supprimé.");
        if (await ctx.AvancementsPhysiques.AnyAsync(a => a.ContratId == contratId))
            return (false, "Un avancement physique est saisi sur ce contrat : il ne peut pas être supprimé.");

        contrat.EstSupprime = true;
        contrat.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesAxes.ModuleAudit, nameof(Contrat), contrat.Id.ToString(),
            new { contrat.Numero, contrat.Objet }, null);
        return (true, null);
    }
}
