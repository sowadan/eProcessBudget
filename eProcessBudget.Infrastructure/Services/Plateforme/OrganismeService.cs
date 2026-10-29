using System.Net.Mail;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Plateforme;

/// <summary>Ligne de la liste des organismes.</summary>
public sealed record LigneOrganisme(
    Ulid Id, string Code, string RaisonSociale, string? Sigle, StatutOrganisme Statut,
    DateOnly? DateActivation, int NbUtilisateurs);

/// <summary>
/// Organismes de la plateforme (plat.Organisme) : création avec provisionnement des données
/// initiales, modification de l'identité, activation, suspension, archivage.
///
/// Réservé à l'Administrateur plateforme (droit OrganismeGerer). Ce service ne lit aucune
/// donnée budgétaire des organismes : il compte seulement leurs utilisateurs.
/// </summary>
public interface IOrganismeService
{
    Task<List<LigneOrganisme>> ListerAsync(string? recherche = null, StatutOrganisme? statut = null);
    Task<Organisme?> ObtenirAsync(Ulid id);

    /// <summary>Organismes actifs ou suspendus, pour un sélecteur (rattachement d'un compte).</summary>
    Task<List<ElementListe>> ListerPourSelectionAsync();

    Task<(bool Ok, string? Erreur, Organisme? Organisme)> CreerAsync(Organisme organisme);
    Task<(bool Ok, string? Erreur)> ModifierAsync(Organisme organisme);

    /// <summary>
    /// Change le statut. Suspendre ou archiver ferme l'accès de tous les comptes de
    /// l'organisme dès leur prochaine requête (validation continue des sessions).
    /// </summary>
    Task<(bool Ok, string? Erreur)> ChangerStatutAsync(Ulid id, StatutOrganisme statut);
}

public class OrganismeService : IOrganismeService
{
    private const string ModuleAudit = "Plateforme";
    private const string MessageDroit = "Seul un Administrateur plateforme peut gérer les organismes.";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IDroitService _droits;
    private readonly IAuditService _audit;

    public OrganismeService(
        IDbContextFactory<AppDbContext> factory,
        IUserContextService utilisateurCourant,
        IDroitService droits,
        IAuditService audit)
    {
        _factory = factory;
        _utilisateurCourant = utilisateurCourant;
        _droits = droits;
        _audit = audit;
    }

    public async Task<List<LigneOrganisme>> ListerAsync(string? recherche = null, StatutOrganisme? statut = null)
    {
        if (await ActeurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.Organismes.AsNoTracking();

        if (statut.HasValue) q = q.Where(o => o.Statut == statut.Value);
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            q = q.Where(o => o.Code.Contains(r) || o.RaisonSociale.Contains(r) || (o.Sigle != null && o.Sigle.Contains(r)));
        }

        return await q.OrderBy(o => o.RaisonSociale)
            .Select(o => new LigneOrganisme(
                o.Id, o.Code, o.RaisonSociale, o.Sigle, o.Statut, o.DateActivation,
                ctx.Utilisateurs.Count(u => u.OrganismeId == o.Id)))
            .ToListAsync();
    }

    public async Task<Organisme?> ObtenirAsync(Ulid id)
    {
        if (await ActeurAsync() == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Organismes.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<ElementListe>> ListerPourSelectionAsync()
    {
        if (await ActeurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Organismes.AsNoTracking()
            .Where(o => o.Statut != StatutOrganisme.Archive)
            .OrderBy(o => o.RaisonSociale)
            .Select(o => new ElementListe(o.Id, o.Code + " — " + o.RaisonSociale))
            .ToListAsync();
    }

    public async Task<(bool, string?, Organisme?)> CreerAsync(Organisme organisme)
    {
        var acteurId = await ActeurAsync();
        if (acteurId == Ulid.Empty) return (false, MessageDroit, null);

        Normaliser(organisme);
        var erreur = Valider(organisme);
        if (erreur is not null) return (false, erreur, null);

        await using var ctx = await _factory.CreateDbContextAsync();

        // L'index unique couvre aussi les organismes supprimés logiquement.
        if (await ctx.Organismes.IgnoreQueryFilters().AnyAsync(o => o.Code == organisme.Code))
            return (false, $"Le code « {organisme.Code} » est déjà utilisé par un autre organisme.", null);

        organisme.Id = Ulid.Empty;
        organisme.Statut = StatutOrganisme.Actif;
        organisme.DateActivation = DateOnly.FromDateTime(DateTime.UtcNow);
        organisme.CreePar = acteurId;
        organisme.DateCreation = DateTime.UtcNow;
        ctx.Organismes.Add(organisme);   // l'ULID est attribué ici, avant l'enregistrement

        // Provisionnement : les lignes appartiennent au nouvel organisme, pas à l'organisme
        // courant (il n'y en a pas pour un compte plateforme). Contournement réservé au
        // module Plateforme (§ 2.3).
        ctx.ContournerFiltreOrganisme = true;
        await ProvisionnementOrganisme.CompleterAsync(ctx, organisme.Id, acteurId);

        // Un seul SaveChanges : l'organisme et ses données initiales, ou rien.
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CREATION", ModuleAudit, nameof(Organisme),
            organisme.Id.ToString(), null, new { organisme.Code, organisme.RaisonSociale, organisme.Sigle });

        return (true, null, organisme);
    }

    public async Task<(bool, string?)> ModifierAsync(Organisme organisme)
    {
        var acteurId = await ActeurAsync();
        if (acteurId == Ulid.Empty) return (false, MessageDroit);

        Normaliser(organisme);
        var erreur = Valider(organisme);
        if (erreur is not null) return (false, erreur);

        await using var ctx = await _factory.CreateDbContextAsync();
        var existant = await ctx.Organismes.FirstOrDefaultAsync(o => o.Id == organisme.Id);
        if (existant is null) return (false, "Organisme introuvable.");

        var avant = new { existant.RaisonSociale, existant.Sigle, existant.Adresse, existant.Telephone, existant.Email,
                          existant.TutelleTechnique, existant.TutelleFinanciere };

        if (organisme.RowVersion is { Length: > 0 })
            ctx.Entry(existant).Property(o => o.RowVersion).OriginalValue = organisme.RowVersion;

        // Le code identifie l'organisme : il n'est pas modifiable.
        existant.RaisonSociale     = organisme.RaisonSociale;
        existant.Sigle             = organisme.Sigle;
        existant.Adresse           = organisme.Adresse;
        existant.Telephone         = organisme.Telephone;
        existant.Email             = organisme.Email;
        existant.TutelleTechnique  = organisme.TutelleTechnique;
        existant.TutelleFinanciere = organisme.TutelleFinanciere;
        existant.ModifiePar        = acteurId;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Cet organisme a été modifié par quelqu'un d'autre entre-temps. Rechargez la page.");
        }

        await _audit.LoggerAsync(acteurId, null, "MODIFICATION", ModuleAudit, nameof(Organisme),
            existant.Id.ToString(), avant,
            new { existant.RaisonSociale, existant.Sigle, existant.Adresse, existant.Telephone, existant.Email,
                  existant.TutelleTechnique, existant.TutelleFinanciere });

        return (true, null);
    }

    public async Task<(bool, string?)> ChangerStatutAsync(Ulid id, StatutOrganisme statut)
    {
        var acteurId = await ActeurAsync();
        if (acteurId == Ulid.Empty) return (false, MessageDroit);
        if (!Enum.IsDefined(statut)) return (false, "Statut inconnu.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var existant = await ctx.Organismes.FirstOrDefaultAsync(o => o.Id == id);
        if (existant is null) return (false, "Organisme introuvable.");
        if (existant.Statut == statut) return (true, null);

        var avant = existant.Statut;
        existant.Statut = statut;
        existant.ModifiePar = acteurId;
        if (statut == StatutOrganisme.Actif) existant.DateActivation ??= DateOnly.FromDateTime(DateTime.UtcNow);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "CHANGEMENT_STATUT", ModuleAudit, nameof(Organisme),
            existant.Id.ToString(), new { Statut = avant.ToString() }, new { Statut = statut.ToString() });

        return (true, null);
    }

    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Utilisateur connecté s'il peut gérer les organismes, sinon Ulid.Empty. Le droit
    /// OrganismeGerer n'est effectif que pour un Administrateur plateforme (IDroitService).
    /// </summary>
    private async Task<Ulid> ActeurAsync()
    {
        var userId = await _utilisateurCourant.ObtenirUserIdAsync();
        return await _droits.ADroitAsync(userId, TypeDroit.OrganismeGerer) ? userId : Ulid.Empty;
    }

    private static string? Valider(Organisme o)
    {
        if (o.Code.Length == 0) return "Le code de l'organisme est obligatoire.";
        if (o.Code.Length > 20) return "Le code ne doit pas dépasser 20 caractères.";
        if (o.RaisonSociale.Length == 0) return "La raison sociale est obligatoire.";
        if (o.RaisonSociale.Length > 200) return "La raison sociale ne doit pas dépasser 200 caractères.";
        if (o.Sigle?.Length > 30) return "Le sigle ne doit pas dépasser 30 caractères.";
        if (o.Adresse?.Length > 300) return "L'adresse ne doit pas dépasser 300 caractères.";
        if (o.Telephone?.Length > 30) return "Le téléphone ne doit pas dépasser 30 caractères.";
        if (o.Email?.Length > 150) return "L'e-mail ne doit pas dépasser 150 caractères.";
        if (o.Email is not null && !MailAddress.TryCreate(o.Email, out _)) return "L'adresse e-mail n'est pas valide.";
        if (o.TutelleTechnique?.Length > 200 || o.TutelleFinanciere?.Length > 200)
            return "Une tutelle ne doit pas dépasser 200 caractères.";
        return null;
    }

    private static void Normaliser(Organisme o)
    {
        static string? Facultatif(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

        o.Code              = (o.Code ?? string.Empty).Trim().ToUpperInvariant();
        o.RaisonSociale     = (o.RaisonSociale ?? string.Empty).Trim();
        o.Sigle             = Facultatif(o.Sigle);
        o.Adresse           = Facultatif(o.Adresse);
        o.Telephone         = Facultatif(o.Telephone);
        o.Email             = Facultatif(o.Email);
        o.TutelleTechnique  = Facultatif(o.TutelleTechnique);
        o.TutelleFinanciere = Facultatif(o.TutelleFinanciere);
    }
}
