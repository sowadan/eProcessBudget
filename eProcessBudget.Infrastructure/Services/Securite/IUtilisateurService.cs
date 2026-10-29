using System.Net.Mail;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>Ligne de la liste des utilisateurs : uniquement les colonnes affichées.</summary>
public sealed record LigneUtilisateur(
    Ulid Id, string Matricule, string NomPrenoms, string Email, string? Fonction,
    string? Organisme, string? Structure, bool EstAdministrateurPlateforme,
    bool EstActif, bool EstBloque, DateTime? DerniereConnexion, IReadOnlyList<string> Profils);

/// <summary>Critères de la liste des utilisateurs.</summary>
/// <param name="OrganismeId">Filtre par organisme (Administrateur plateforme uniquement).</param>
public sealed record FiltreUtilisateurs(
    string? Recherche = null, bool? EstActif = null, Ulid? OrganismeId = null, int Page = 1, int Taille = 25);

/// <summary>
/// Gestion des comptes utilisateurs, pour l'écran /administration/utilisateurs.
///
/// Chaque méthode contrôle elle-même les droits de l'utilisateur connecté et son
/// périmètre : un administrateur d'organisme ne voit et ne gère que les comptes de son
/// organisme ; un Administrateur plateforme gère tous les comptes, y compris les comptes
/// plateforme. Toutes les écritures sont auditées.
/// </summary>
public interface IUtilisateurService
{
    Task<(List<LigneUtilisateur> Items, int Total)> RechercherAsync(FiltreUtilisateurs filtre);

    /// <summary>Compte à modifier (sans relation), ou null s'il est hors du périmètre.</summary>
    Task<Utilisateur?> ObtenirAsync(Ulid id);

    /// <summary>Identifiants des profils actuellement attribués (non expirés) à l'utilisateur.</summary>
    Task<IReadOnlySet<Ulid>> ObtenirProfilsAsync(Ulid utilisateurId);

    /// <summary>Structures actives de l'organisme de l'utilisateur connecté (rattachement).</summary>
    Task<List<ElementListe>> ListerStructuresAsync();

    /// <summary>
    /// Crée un compte. <paramref name="profilIds"/> null = aucun profil ;
    /// <paramref name="motDePasse"/> null = pas encore de mot de passe (lien de réinitialisation).
    /// </summary>
    Task<(bool Ok, string? Erreur, Utilisateur? Utilisateur)> CreerAsync(
        Utilisateur utilisateur, IEnumerable<Ulid>? profilIds, string? motDePasse);

    /// <summary>
    /// Met à jour l'identité, la structure et l'état du compte. <paramref name="profilIds"/>
    /// null = profils inchangés ; sinon remplace la liste (droit ProfilAttribution).
    /// </summary>
    Task<(bool Ok, string? Erreur)> ModifierAsync(Utilisateur utilisateur, IEnumerable<Ulid>? profilIds);

    /// <summary>Définit un mot de passe (provisoire) ; ferme les sessions ouvertes du compte.</summary>
    Task<(bool Ok, string? Erreur)> DefinirMotDePasseAsync(Ulid utilisateurId, string? motDePasse);

    /// <summary>Lève le blocage consécutif aux échecs de connexion.</summary>
    Task<(bool Ok, string? Erreur)> DebloquerAsync(Ulid utilisateurId);
}

/// <summary>Utilisateurs persistés dans les tables auth.Utilisateur / auth.UtilisateurProfil.</summary>
public class UtilisateurServiceEf : IUtilisateurService
{
    private const string ModuleAudit = "Securite";
    private const string MessageDroitGestion = "Vous n'avez pas le droit de gérer les utilisateurs.";
    private const string MessageIntrouvable = "Utilisateur introuvable.";
    private const int TailleMax = 200;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IDroitService _droits;
    private readonly IPasswordHasher<Utilisateur> _hacheur;
    private readonly Audit.IAuditService _audit;
    private readonly SecuriteOptions _securite;

    public UtilisateurServiceEf(
        IDbContextFactory<AppDbContext> factory,
        IUserContextService utilisateurCourant,
        IDroitService droits,
        IPasswordHasher<Utilisateur> hacheur,
        Audit.IAuditService audit,
        IOptions<SecuriteOptions> securite)
    {
        _factory = factory;
        _utilisateurCourant = utilisateurCourant;
        _droits = droits;
        _hacheur = hacheur;
        _audit = audit;
        _securite = securite.Value;
    }

    /// <summary>Utilisateur connecté qui agit, avec ce qui borne son périmètre.</summary>
    private sealed record Acteur(Ulid Id, Ulid? OrganismeId, bool EstPlateforme);

    // ═════════════════════════════════════════════════════════════════════════
    //  Lecture
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(List<LigneUtilisateur> Items, int Total)> RechercherAsync(FiltreUtilisateurs filtre)
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurConsultation, TypeDroit.UtilisateurGestion);
        if (acteur is null) return ([], 0);

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = Perimetre(ctx.Utilisateurs.AsNoTracking(), acteur);

        if (filtre.EstActif.HasValue) q = q.Where(u => u.EstActif == filtre.EstActif.Value);
        if (acteur.EstPlateforme && filtre.OrganismeId.HasValue) q = q.Where(u => u.OrganismeId == filtre.OrganismeId);

        if (!string.IsNullOrWhiteSpace(filtre.Recherche))
        {
            var r = filtre.Recherche.Trim();
            q = q.Where(u => u.Matricule.Contains(r) || u.NomPrenoms.Contains(r) || u.Email.Contains(r));
        }

        var total = await q.CountAsync();
        var taille = Math.Clamp(filtre.Taille, 1, TailleMax);
        var maintenant = DateTime.UtcNow;

        var items = await q.OrderBy(u => u.NomPrenoms)
            .Skip(Math.Max(0, (filtre.Page - 1) * taille))
            .Take(taille)
            .Select(u => new LigneUtilisateur(
                u.Id, u.Matricule, u.NomPrenoms, u.Email, u.Fonction,
                u.Organisme == null ? null : u.Organisme.Code,
                // Structure : table d'organisme, visible uniquement depuis cet organisme.
                u.Structure == null ? null : u.Structure.Libelle,
                u.EstAdministrateurPlateforme,
                u.EstActif,
                u.BloqueJusquAu > maintenant,
                u.DerniereConnexion,
                u.UtilisateurProfils
                    .Where(up => up.DateFin == null || up.DateFin >= maintenant)
                    .Select(up => up.Profil.Libelle)
                    .OrderBy(l => l)
                    .ToList()))
            .ToListAsync();

        return (items, total);
    }

    public async Task<Utilisateur?> ObtenirAsync(Ulid id)
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurConsultation, TypeDroit.UtilisateurGestion);
        if (acteur is null) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        return await Perimetre(ctx.Utilisateurs.AsNoTracking(), acteur).FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<IReadOnlySet<Ulid>> ObtenirProfilsAsync(Ulid utilisateurId)
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurConsultation, TypeDroit.UtilisateurGestion);
        if (acteur is null) return new HashSet<Ulid>();

        var maintenant = DateTime.UtcNow;
        await using var ctx = await _factory.CreateDbContextAsync();
        var ids = await Perimetre(ctx.Utilisateurs.AsNoTracking(), acteur)
            .Where(u => u.Id == utilisateurId)
            .SelectMany(u => u.UtilisateurProfils)
            .Where(up => up.DateFin == null || up.DateFin >= maintenant)
            .Select(up => up.ProfilId)
            .ToListAsync();
        return ids.ToHashSet();
    }

    public async Task<List<ElementListe>> ListerStructuresAsync()
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurGestion);
        if (acteur is null || acteur.EstPlateforme) return [];

        // Le contexte est déjà restreint à l'organisme de l'utilisateur connecté.
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Structures.AsNoTracking()
            .Where(s => s.EstActif)
            .OrderBy(s => s.Libelle)
            .Select(s => new ElementListe(s.Id, s.Code + " — " + s.Libelle))
            .ToListAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Écriture
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<(bool, string?, Utilisateur?)> CreerAsync(
        Utilisateur utilisateur, IEnumerable<Ulid>? profilIds, string? motDePasse)
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurGestion);
        if (acteur is null) return (false, MessageDroitGestion, null);

        Normaliser(utilisateur);
        var erreur = ValiderIdentite(utilisateur);
        if (utilisateur.Matricule.Length == 0) erreur ??= "L'identifiant de connexion est obligatoire.";
        else if (utilisateur.Matricule.Length > 100) erreur ??= "L'identifiant de connexion ne doit pas dépasser 100 caractères.";
        if (erreur is not null) return (false, erreur, null);

        await using var ctx = await _factory.CreateDbContextAsync();

        // ── Rattachement : le périmètre de l'acteur décide, pas la saisie ─────
        if (acteur.EstPlateforme)
        {
            utilisateur.StructureId = null;   // les structures relèvent de chaque organisme
            if (utilisateur.EstAdministrateurPlateforme)
            {
                utilisateur.OrganismeId = null;
            }
            else
            {
                if (utilisateur.OrganismeId is null) return (false, "Choisissez l'organisme de rattachement.", null);
                var statut = await ctx.Organismes.AsNoTracking()
                    .Where(o => o.Id == utilisateur.OrganismeId)
                    .Select(o => (StatutOrganisme?)o.Statut)
                    .FirstOrDefaultAsync();
                if (statut is null) return (false, "Organisme introuvable.", null);
                if (statut == StatutOrganisme.Archive) return (false, "Cet organisme est archivé.", null);
            }
        }
        else
        {
            utilisateur.EstAdministrateurPlateforme = false;
            utilisateur.OrganismeId = acteur.OrganismeId;
            if (!await StructureValideAsync(ctx, utilisateur.StructureId))
                return (false, "Structure de rattachement introuvable.", null);
        }

        // ── Unicité (comptes supprimés compris : les index uniques les couvrent) ──
        var conflit = await VerifierUniciteAsync(ctx, utilisateur, verifierMatricule: true);
        if (conflit is not null) return (false, conflit, null);

        // ── Profils ──────────────────────────────────────────────────────────
        var profils = (profilIds ?? []).Distinct().ToList();
        if (profils.Count > 0)
        {
            if (!await _droits.ADroitAsync(acteur.Id, TypeDroit.ProfilAttribution))
                return (false, "Le droit « attribution de profils » est nécessaire pour attribuer des profils.", null);
            var erreurProfils = await ValiderProfilsAsync(ctx, profils, utilisateur.EstAdministrateurPlateforme);
            if (erreurProfils is not null) return (false, erreurProfils, null);
        }

        // ── Mot de passe initial (facultatif) ─────────────────────────────────
        string? empreinte = null;
        if (!string.IsNullOrEmpty(motDePasse))
        {
            var erreurMotDePasse = PolitiqueMotDePasse.Verifier(motDePasse, _securite.MotDePasse);
            if (erreurMotDePasse is not null) return (false, erreurMotDePasse, null);
            empreinte = await Task.Run(() => _hacheur.HashPassword(utilisateur, motDePasse));
        }

        var maintenant = DateTime.UtcNow;

        // Champs gérés par l'application : jamais repris de la saisie.
        utilisateur.Id = Ulid.Empty;
        utilisateur.PasswordHash = empreinte;
        utilisateur.NbEchecsConnexion = 0;
        utilisateur.BloqueJusquAu = null;
        utilisateur.DerniereConnexion = null;
        utilisateur.CreePar = acteur.Id;
        utilisateur.DateCreation = maintenant;
        utilisateur.UtilisateurProfils = profils
            .Select(id => new UtilisateurProfil
            {
                ProfilId    = id,
                DateDebut   = maintenant,
                AttribuePar = acteur.Id,
                CreePar     = acteur.Id
            })
            .ToList();

        // Un seul SaveChanges : le compte et ses profils sont créés dans la même transaction.
        ctx.Utilisateurs.Add(utilisateur);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteur.Id, null, "CREATION", ModuleAudit, nameof(Utilisateur),
            utilisateur.Id.ToString(), null,
            new
            {
                utilisateur.Matricule, utilisateur.NomPrenoms, utilisateur.Email, utilisateur.OrganismeId,
                utilisateur.EstAdministrateurPlateforme, utilisateur.EstActif, Profils = profils,
                MotDePasseDefini = empreinte is not null
            },
            organismeId: utilisateur.OrganismeId);

        return (true, null, utilisateur);
    }

    public async Task<(bool, string?)> ModifierAsync(Utilisateur utilisateur, IEnumerable<Ulid>? profilIds)
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurGestion);
        if (acteur is null) return (false, MessageDroitGestion);

        Normaliser(utilisateur);
        var erreur = ValiderIdentite(utilisateur);
        if (erreur is not null) return (false, erreur);

        await using var ctx = await _factory.CreateDbContextAsync();
        var existant = await Perimetre(ctx.Utilisateurs, acteur).FirstOrDefaultAsync(u => u.Id == utilisateur.Id);
        if (existant is null) return (false, MessageIntrouvable);

        // Garde-fous contre le verrouillage : on ne se retire pas soi-même l'accès.
        if (existant.Id == acteur.Id && !utilisateur.EstActif)
            return (false, "Vous ne pouvez pas désactiver votre propre compte.");

        List<Ulid>? profils = null;
        if (profilIds is not null)
        {
            if (!await _droits.ADroitAsync(acteur.Id, TypeDroit.ProfilAttribution))
                return (false, "Le droit « attribution de profils » est nécessaire pour modifier les profils.");

            profils = profilIds.Distinct().ToList();
            var erreurProfils = await ValiderProfilsAsync(ctx, profils, existant.EstAdministrateurPlateforme);
            if (erreurProfils is not null) return (false, erreurProfils);

            if (existant.Id == acteur.Id && await RetireProfilAdministrationAsync(ctx, existant.Id, profils))
                return (false, "Vous ne pouvez pas retirer votre propre profil d'administration.");
        }

        if (!acteur.EstPlateforme && !await StructureValideAsync(ctx, utilisateur.StructureId))
            return (false, "Structure de rattachement introuvable.");

        utilisateur.Matricule = existant.Matricule;
        var conflit = await VerifierUniciteAsync(ctx, utilisateur, verifierMatricule: false);
        if (conflit is not null) return (false, conflit);

        var avant = new { existant.NomPrenoms, existant.Email, existant.Fonction, existant.StructureId, existant.EstActif };

        // Concurrence : si le compte a changé depuis son ouverture à l'écran, on refuse.
        if (utilisateur.RowVersion is { Length: > 0 })
            ctx.Entry(existant).Property(u => u.RowVersion).OriginalValue = utilisateur.RowVersion;

        existant.NomPrenoms = utilisateur.NomPrenoms;
        existant.Email      = utilisateur.Email;
        existant.Fonction   = utilisateur.Fonction;
        existant.EstActif   = utilisateur.EstActif;
        existant.ModifiePar = acteur.Id;
        if (!acteur.EstPlateforme) existant.StructureId = utilisateur.StructureId;

        if (profils is not null)
            await AppliquerProfilsAsync(ctx, existant.Id, profils, acteur.Id);

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Ce compte a été modifié par quelqu'un d'autre entre-temps. Rechargez la page.");
        }

        await _audit.LoggerAsync(acteur.Id, null, "MODIFICATION", ModuleAudit, nameof(Utilisateur),
            existant.Id.ToString(), avant,
            new { existant.NomPrenoms, existant.Email, existant.Fonction, existant.StructureId, existant.EstActif, Profils = profils },
            organismeId: existant.OrganismeId);

        // Profils ou état changés : les droits en cache ne sont plus à jour.
        _droits.InvaliderCache(existant.Id);

        return (true, null);
    }

    public async Task<(bool, string?)> DefinirMotDePasseAsync(Ulid utilisateurId, string? motDePasse)
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurGestion);
        if (acteur is null) return (false, MessageDroitGestion);
        if (utilisateurId == acteur.Id)
            return (false, "Pour votre propre compte, utilisez « Mon mot de passe » : l'actuel vous sera demandé.");

        var erreurPolitique = PolitiqueMotDePasse.Verifier(motDePasse, _securite.MotDePasse);
        if (erreurPolitique is not null) return (false, erreurPolitique);

        await using var ctx = await _factory.CreateDbContextAsync();
        var compte = await Perimetre(ctx.Utilisateurs, acteur).FirstOrDefaultAsync(u => u.Id == utilisateurId);
        if (compte is null) return (false, MessageIntrouvable);

        compte.PasswordHash = await Task.Run(() => _hacheur.HashPassword(compte, motDePasse!));
        compte.NbEchecsConnexion = 0;
        compte.BloqueJusquAu = null;
        compte.ModifiePar = acteur.Id;
        await ctx.SaveChangesAsync();

        // Sessions ouvertes du compte : fermées dès leur prochaine vérification.
        _droits.InvaliderCache(compte.Id);

        // Jamais le mot de passe ni son empreinte dans l'audit (§ 2.7).
        await _audit.LoggerAsync(acteur.Id, null, "DEFINITION_MOT_DE_PASSE", ModuleAudit, nameof(Utilisateur),
            compte.Id.ToString(), organismeId: compte.OrganismeId);

        return (true, null);
    }

    public async Task<(bool, string?)> DebloquerAsync(Ulid utilisateurId)
    {
        var acteur = await ActeurAsync(TypeDroit.UtilisateurGestion);
        if (acteur is null) return (false, MessageDroitGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var compte = await Perimetre(ctx.Utilisateurs, acteur).FirstOrDefaultAsync(u => u.Id == utilisateurId);
        if (compte is null) return (false, MessageIntrouvable);

        var avant = new { compte.BloqueJusquAu, compte.NbEchecsConnexion };
        compte.BloqueJusquAu = null;
        compte.NbEchecsConnexion = 0;
        compte.ModifiePar = acteur.Id;
        await ctx.SaveChangesAsync();

        _droits.InvaliderCache(compte.Id);

        await _audit.LoggerAsync(acteur.Id, null, "DEBLOCAGE", ModuleAudit, nameof(Utilisateur),
            compte.Id.ToString(), avant, null, organismeId: compte.OrganismeId);

        return (true, null);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Règles
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>L'utilisateur connecté s'il est actif et possède l'un des droits, sinon null.</summary>
    private async Task<Acteur?> ActeurAsync(params TypeDroit[] droits)
    {
        var u = await _utilisateurCourant.ObtenirUtilisateurAsync();
        if (u is not { EstActif: true }) return null;
        if (!await _droits.ADroitAsync(u.Id, droits)) return null;
        return new Acteur(u.Id, u.OrganismeId, u.EstAdministrateurPlateforme);
    }

    /// <summary>
    /// Comptes visibles par l'acteur : tous pour un Administrateur plateforme ; ceux de son
    /// organisme, comptes plateforme exclus, pour un administrateur d'organisme.
    /// </summary>
    private static IQueryable<Utilisateur> Perimetre(IQueryable<Utilisateur> q, Acteur acteur)
        => acteur.EstPlateforme
            ? q
            : q.Where(u => u.OrganismeId == acteur.OrganismeId && !u.EstAdministrateurPlateforme);

    private static string? ValiderIdentite(Utilisateur u)
    {
        if (u.NomPrenoms.Length == 0) return "Le nom et les prénoms sont obligatoires.";
        if (u.NomPrenoms.Length > 300) return "Le nom et les prénoms ne doivent pas dépasser 300 caractères.";
        if (u.Fonction?.Length > 150) return "La fonction ne doit pas dépasser 150 caractères.";
        if (u.Email.Length > 150) return "L'e-mail ne doit pas dépasser 150 caractères.";
        if (u.Email.Length > 0 && !MailAddress.TryCreate(u.Email, out _)) return "L'adresse e-mail n'est pas valide.";
        return null;
    }

    private static async Task<string?> VerifierUniciteAsync(AppDbContext ctx, Utilisateur u, bool verifierMatricule)
    {
        if (verifierMatricule)
        {
            var existant = await ctx.Utilisateurs.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.Matricule == u.Matricule)
                .Select(x => new { x.EstSupprime })
                .FirstOrDefaultAsync();
            if (existant is not null)
                return existant.EstSupprime
                    ? $"L'identifiant « {u.Matricule} » appartient à un compte supprimé."
                    : $"L'identifiant « {u.Matricule} » est déjà utilisé.";
        }

        if (u.Email.Length > 0
            && await ctx.Utilisateurs.IgnoreQueryFilters().AnyAsync(x => x.Email == u.Email && x.Id != u.Id))
            return $"L'adresse « {u.Email} » est déjà utilisée par un autre compte.";

        return null;
    }

    /// <summary>La structure (facultative) existe dans l'organisme courant du contexte.</summary>
    private static async Task<bool> StructureValideAsync(AppDbContext ctx, Ulid? structureId)
        => structureId is null || await ctx.Structures.AnyAsync(s => s.Id == structureId && s.EstActif);

    private static async Task<string?> ValiderProfilsAsync(AppDbContext ctx, List<Ulid> profils, bool comptePlateforme)
    {
        if (profils.Count == 0) return null;

        var codes = await ctx.Profils.AsNoTracking()
            .Where(p => profils.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync();

        if (codes.Count != profils.Count) return "Un des profils choisis n'existe plus.";
        if (!comptePlateforme && codes.Contains(SeedService.CodeProfilAdministrateurPlateforme))
            return "Le profil Administrateur plateforme est réservé aux comptes de niveau plateforme.";
        return null;
    }

    /// <summary>true si la nouvelle liste retire à l'utilisateur un profil d'administration qu'il détient.</summary>
    private async Task<bool> RetireProfilAdministrationAsync(AppDbContext ctx, Ulid utilisateurId, List<Ulid> cibles)
    {
        string[] codesAdministration = [SeedService.CodeProfilAdministrateur, SeedService.CodeProfilAdministrateurPlateforme];
        var idsAdministration = await ctx.Profils.AsNoTracking()
            .Where(p => codesAdministration.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync();

        var actuels = await ObtenirProfilsAsync(utilisateurId);
        return idsAdministration.Any(id => actuels.Contains(id) && !cibles.Contains(id));
    }

    /// <summary>
    /// Aligne les profils en vigueur de l'utilisateur sur <paramref name="cibles"/>.
    /// Un profil retiré est supprimé logiquement (la trace reste en base et dans l'audit).
    /// Ne sauvegarde pas : l'appelant fait le SaveChanges.
    /// </summary>
    private static async Task AppliquerProfilsAsync(AppDbContext ctx, Ulid utilisateurId, List<Ulid> cibles, Ulid acteurId)
    {
        var maintenant = DateTime.UtcNow;

        var actuels = await ctx.UtilisateurProfils
            .Where(up => up.UtilisateurId == utilisateurId
                      && (up.DateFin == null || up.DateFin >= maintenant))
            .ToListAsync();

        foreach (var up in actuels.Where(up => !cibles.Contains(up.ProfilId)))
        {
            up.EstSupprime = true;
            up.DateSuppression = maintenant;
            up.SupprimePar = acteurId;
        }

        var dejaPresents = actuels.Select(up => up.ProfilId).ToHashSet();
        foreach (var profilId in cibles.Where(id => !dejaPresents.Contains(id)))
        {
            ctx.UtilisateurProfils.Add(new UtilisateurProfil
            {
                UtilisateurId = utilisateurId,
                ProfilId      = profilId,
                DateDebut     = maintenant,
                AttribuePar   = acteurId,
                CreePar       = acteurId
            });
        }
    }

    private static void Normaliser(Utilisateur u)
    {
        u.Matricule  = (u.Matricule ?? string.Empty).Trim();
        u.NomPrenoms = (u.NomPrenoms ?? string.Empty).Trim();
        u.Email      = (u.Email ?? string.Empty).Trim();
        u.Fonction   = string.IsNullOrWhiteSpace(u.Fonction) ? null : u.Fonction.Trim();
    }
}
