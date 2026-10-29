using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Notifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eProcessBudget.Infrastructure.Services.Securite;

/// <summary>Issue d'une tentative de connexion.</summary>
/// <param name="Principal">Identité à placer dans le cookie (succès uniquement).</param>
/// <param name="DureeSession">Expiration glissante applicable à ce compte.</param>
public sealed record ResultatConnexion(
    bool Ok, string? Erreur, ClaimsPrincipal? Principal = null, TimeSpan DureeSession = default);

/// <summary>
/// Authentification par identifiant et mot de passe (cahier fonctionnel § 3.1.2, technique § 2.6) :
/// vérification BCrypt, blocage après N échecs, journal des connexions, réinitialisation du
/// mot de passe par e-mail et validation continue des sessions ouvertes.
///
/// Ce service ne pose pas le cookie : il renvoie l'identité, que la couche Web (seule à
/// disposer de la requête HTTP) transmet à SignInAsync.
/// </summary>
public interface IAuthentificationService
{
    /// <summary>Vérifie les identifiants, met à jour les compteurs d'échec et journalise la tentative.</summary>
    Task<ResultatConnexion> ConnecterAsync(string? identifiant, string? motDePasse, string adresseIp, string? userAgent);

    /// <summary>Journalise la fermeture volontaire (ou par inactivité) d'une session.</summary>
    Task JournaliserDeconnexionAsync(ClaimsPrincipal principal, string adresseIp, string? userAgent, string? motif);

    /// <summary>
    /// true si la session portée par <paramref name="principal"/> est toujours acceptable :
    /// compte actif et non bloqué, empreinte inchangée, organisme actif.
    /// </summary>
    Task<bool> ValiderSessionAsync(ClaimsPrincipal principal);

    /// <summary>Durée d'inactivité admise pour l'organisme (paramètre de l'organisme, sinon configuration).</summary>
    Task<TimeSpan> ObtenirDureeSessionAsync(Ulid organismeId);

    /// <summary>
    /// Envoie un lien de réinitialisation si un compte actif correspond. Ne dit jamais si le
    /// compte existe : la page affiche le même message dans tous les cas.
    /// </summary>
    /// <param name="urlBase">Adresse publique de l'application ; null si elle n'est pas configurée (aucun lien envoyé).</param>
    Task DemanderReinitialisationAsync(string? identifiant, string? urlBase);

    /// <summary>true si le lien est authentique, non expiré et pas encore utilisé.</summary>
    Task<bool> JetonReinitialisationValideAsync(string? jeton);

    Task<(bool Ok, string? Erreur)> ReinitialiserMotDePasseAsync(string? jeton, string? nouveauMotDePasse);

    /// <summary>Changement de son propre mot de passe par l'utilisateur connecté.</summary>
    Task<(bool Ok, string? Erreur)> ChangerMotDePasseAsync(string? motDePasseActuel, string? nouveauMotDePasse);

    /// <summary>Rappel de la politique de mot de passe, pour l'affichage.</summary>
    string DescriptionPolitique { get; }
}

public class AuthentificationService : IAuthentificationService
{
    private const string MessageIdentifiantsIncorrects = "Identifiant ou mot de passe incorrect.";
    private const string MessageLienInvalide = "Ce lien de réinitialisation est invalide, expiré ou déjà utilisé.";
    private const string ButProtection = "eProcessBudget.Securite.ReinitialisationMotDePasse.v1";
    private const string ModuleAudit = "Securite";

    /// <summary>
    /// Résultat d'une validation de session mis en cache : le cookie est revérifié à chaque
    /// requête HTTP, ressources statiques comprises ; on évite une lecture en base par fichier.
    /// </summary>
    private static readonly TimeSpan DureeCacheSession = TimeSpan.FromSeconds(20);

    // Empreinte d'un mot de passe fictif : un identifiant inconnu coûte le même temps de
    // calcul qu'un mot de passe faux, ce qui ne laisse pas deviner l'existence d'un compte.
    private static string? _empreinteFictive;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IPasswordHasher<Utilisateur> _hacheur;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IAuditService _audit;
    private readonly IEmailQueue _emails;
    private readonly IMemoryCache _cache;
    private readonly ITimeLimitedDataProtector _protecteur;
    private readonly SecuriteOptions _options;
    private readonly EmailOptions _emailOptions;
    private readonly ApplicationOptions _application;
    private readonly ILogger<AuthentificationService> _log;

    public AuthentificationService(
        [FromKeyedServices(FabriqueContexteOrganisme.CleSansOrganisme)] IDbContextFactory<AppDbContext> factory,
        IPasswordHasher<Utilisateur> hacheur,
        IUserContextService utilisateurCourant,
        IAuditService audit,
        IEmailQueue emails,
        IMemoryCache cache,
        IDataProtectionProvider protection,
        IOptions<SecuriteOptions> options,
        IOptions<EmailOptions> emailOptions,
        IOptions<ApplicationOptions> application,
        ILogger<AuthentificationService> log)
    {
        _factory = factory;
        _hacheur = hacheur;
        _utilisateurCourant = utilisateurCourant;
        _audit = audit;
        _emails = emails;
        _cache = cache;
        _protecteur = protection.CreateProtector(ButProtection).ToTimeLimitedDataProtector();
        _options = options.Value;
        _emailOptions = emailOptions.Value;
        _application = application.Value;
        _log = log;
    }

    public string DescriptionPolitique => PolitiqueMotDePasse.Description(_options.MotDePasse);

    // ═════════════════════════════════════════════════════════════════════════
    //  Connexion
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<ResultatConnexion> ConnecterAsync(
        string? identifiant, string? motDePasse, string adresseIp, string? userAgent)
    {
        var login = (identifiant ?? string.Empty).Trim();
        if (login.Length == 0 || string.IsNullOrEmpty(motDePasse))
            return new(false, "Saisissez votre identifiant et votre mot de passe.");

        var maintenant = DateTime.UtcNow;
        await using var ctx = await _factory.CreateDbContextAsync();

        var compte = await ctx.Utilisateurs
            .Where(u => u.Matricule == login || (u.Email != "" && u.Email == login))
            .OrderBy(u => u.Matricule == login ? 0 : 1)
            .FirstOrDefaultAsync();

        var tentative = new JournalConnexion
        {
            Login     = Tronquer(login, 150)!,
            AdresseIp = Tronquer(adresseIp, 45) ?? "inconnue",
            UserAgent = Tronquer(userAgent, 400),
            DateHeure = maintenant
        };

        if (compte is null)
        {
            await Task.Run(() => _hacheur.VerifyHashedPassword(null!, ObtenirEmpreinteFictive(), motDePasse));
            await JournaliserAsync(ctx, tentative, StatutConnexion.Echec, "Identifiant inconnu");
            return new(false, MessageIdentifiantsIncorrects);
        }

        tentative.UtilisateurId = compte.Id;
        tentative.OrganismeId = compte.OrganismeId;

        // Compte bloqué : on ne vérifie même pas le mot de passe, et la tentative ne
        // prolonge pas le blocage.
        if (compte.BloqueJusquAu > maintenant)
        {
            await JournaliserAsync(ctx, tentative, StatutConnexion.CompteBloque, "Tentative pendant le blocage");
            return new(false, MessageBlocage(compte.BloqueJusquAu.Value - maintenant));
        }

        var verification = await Task.Run(() =>
            _hacheur.VerifyHashedPassword(compte, compte.PasswordHash ?? string.Empty, motDePasse));

        var (nbTentativesMax, dureeSession) = await LireParametresAsync(compte.OrganismeId);

        if (verification == PasswordVerificationResult.Failed)
        {
            var finBlocage = maintenant.AddMinutes(Math.Max(1, _options.DureeBlocageMinutes));
            var bloque = false;

            await ModifierCompteAsync(ctx, compte, u =>
            {
                u.NbEchecsConnexion++;
                bloque = u.NbEchecsConnexion >= nbTentativesMax;
                if (bloque)
                {
                    u.BloqueJusquAu = finBlocage;
                    u.NbEchecsConnexion = 0;
                }
            });

            if (bloque)
            {
                await JournaliserAsync(ctx, tentative, StatutConnexion.CompteBloque,
                    $"Blocage après {nbTentativesMax} échecs consécutifs");
                return new(false, MessageBlocage(finBlocage - maintenant));
            }

            await JournaliserAsync(ctx, tentative, StatutConnexion.Echec, "Mot de passe incorrect");
            return new(false, MessageIdentifiantsIncorrects);
        }

        // Mot de passe correct : on peut expliquer précisément un refus sans rien révéler.
        var refus = await MotifRefusAsync(ctx, compte);
        if (refus is not null)
        {
            await JournaliserAsync(ctx, tentative, StatutConnexion.Echec, refus);
            return new(false, refus);
        }

        var nouvelleEmpreinte = verification == PasswordVerificationResult.SuccessRehashNeeded
            ? await Task.Run(() => _hacheur.HashPassword(compte, motDePasse))
            : null;

        await ModifierCompteAsync(ctx, compte, u =>
        {
            u.NbEchecsConnexion = 0;
            u.BloqueJusquAu = null;
            u.DerniereConnexion = maintenant;
            if (nouvelleEmpreinte is not null) u.PasswordHash = nouvelleEmpreinte;
        });

        await JournaliserAsync(ctx, tentative, StatutConnexion.Succes, null);

        return new(true, null, Revendications.CreerPrincipal(compte), TimeSpan.FromMinutes(dureeSession));
    }

    public async Task JournaliserDeconnexionAsync(
        ClaimsPrincipal principal, string adresseIp, string? userAgent, string? motif)
    {
        var id = principal.UtilisateurId();
        if (id == Ulid.Empty) return;

        await using var ctx = await _factory.CreateDbContextAsync();
        var compte = await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.Matricule, u.OrganismeId })
            .FirstOrDefaultAsync();
        if (compte is null) return;

        await JournaliserAsync(ctx, new JournalConnexion
        {
            UtilisateurId = id,
            OrganismeId   = compte.OrganismeId,
            Login         = compte.Matricule,
            AdresseIp     = Tronquer(adresseIp, 45) ?? "inconnue",
            UserAgent     = Tronquer(userAgent, 400),
            DateHeure     = DateTime.UtcNow
        }, StatutConnexion.Deconnexion, Tronquer(motif, 200));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Sessions
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<bool> ValiderSessionAsync(ClaimsPrincipal principal)
    {
        var id = principal.UtilisateurId();
        var empreinte = principal.FindFirstValue(Revendications.Empreinte);
        if (id == Ulid.Empty || string.IsNullOrEmpty(empreinte)) return false;

        // Le cache mémorise l'empreinte vérifiée : une session ouverte avec une autre
        // empreinte (avant un changement de mot de passe) n'en profite pas.
        var cle = CacheSecurite.CleSession(id);
        if (_cache.TryGetValue(cle, out (string Empreinte, bool Valide) memo) && memo.Empreinte == empreinte)
            return memo.Valide;

        await using var ctx = await _factory.CreateDbContextAsync();
        var etat = await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id, u.PasswordHash, u.OrganismeId, u.EstAdministrateurPlateforme, u.EstActif, u.BloqueJusquAu,
                StatutOrganisme = u.Organisme == null ? (StatutOrganisme?)null : u.Organisme.Statut
            })
            .FirstOrDefaultAsync();

        var valide = etat is not null
              && etat.EstActif
              && !(etat.BloqueJusquAu > DateTime.UtcNow)
              && (etat.EstAdministrateurPlateforme || etat.StatutOrganisme == StatutOrganisme.Actif)
              && Revendications.CalculerEmpreinte(etat.Id, etat.PasswordHash, etat.OrganismeId,
                                                  etat.EstAdministrateurPlateforme) == empreinte;

        _cache.Set(cle, (empreinte, valide), DureeCacheSession);
        return valide;
    }

    public async Task<TimeSpan> ObtenirDureeSessionAsync(Ulid organismeId)
    {
        var (_, minutes) = await LireParametresAsync(organismeId == Ulid.Empty ? null : organismeId);
        return TimeSpan.FromMinutes(minutes);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Mots de passe
    // ═════════════════════════════════════════════════════════════════════════

    public async Task DemanderReinitialisationAsync(string? identifiant, string? urlBase)
    {
        var cle = (identifiant ?? string.Empty).Trim();
        if (cle.Length == 0) return;

        await using var ctx = await _factory.CreateDbContextAsync();
        var compte = await ctx.Utilisateurs.AsNoTracking()
            .Where(u => u.EstActif && u.Email != "" && (u.Email == cle || u.Matricule == cle))
            .FirstOrDefaultAsync();

        if (compte is null) return;

        if (!_emailOptions.Active)
        {
            _log.LogWarning("Réinitialisation du mot de passe demandée pour le compte {UtilisateurId}, " +
                            "mais l'envoi d'e-mails est désactivé (Email:Active) : aucun lien envoyé.", compte.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(urlBase))
        {
            _log.LogWarning("Réinitialisation du mot de passe demandée pour le compte {UtilisateurId}, " +
                            "mais Email:BaseUrl n'est pas renseignée : aucun lien envoyé.", compte.Id);
            return;
        }

        var empreinte = Revendications.CalculerEmpreinte(compte.Id, compte.PasswordHash, compte.OrganismeId,
                                                         compte.EstAdministrateurPlateforme);
        var duree = TimeSpan.FromMinutes(Math.Max(5, _options.DureeLienReinitialisationMinutes));
        var jeton = _protecteur.Protect($"{compte.Id}|{empreinte}", duree);
        var lien = $"{urlBase.TrimEnd('/')}/compte/reinitialiser?jeton={Uri.EscapeDataString(jeton)}";

        _emails.Enqueue(new EmailMessage
        {
            Destinataires = [compte.Email],
            Sujet         = $"{_application.Nom} : réinitialisation de votre mot de passe",
            CorpsHtml     = CorpsEmailReinitialisation(compte.NomPrenoms, lien, (int)duree.TotalMinutes)
        });

        await _audit.LoggerAsync(compte.Id, compte.Matricule, "DEMANDE_REINITIALISATION", ModuleAudit,
            nameof(Utilisateur), compte.Id.ToString(), organismeId: compte.OrganismeId);
    }

    public async Task<bool> JetonReinitialisationValideAsync(string? jeton)
    {
        var contenu = LireJeton(jeton);
        if (contenu is null) return false;

        await using var ctx = await _factory.CreateDbContextAsync();
        var compte = await ctx.Utilisateurs.AsNoTracking().FirstOrDefaultAsync(u => u.Id == contenu.Value.Id);
        return compte is { EstActif: true } && EmpreinteDe(compte) == contenu.Value.Empreinte;
    }

    public async Task<(bool Ok, string? Erreur)> ReinitialiserMotDePasseAsync(string? jeton, string? nouveauMotDePasse)
    {
        var contenu = LireJeton(jeton);
        if (contenu is null) return (false, MessageLienInvalide);

        var erreurPolitique = PolitiqueMotDePasse.Verifier(nouveauMotDePasse, _options.MotDePasse);
        if (erreurPolitique is not null) return (false, erreurPolitique);

        await using var ctx = await _factory.CreateDbContextAsync();
        var compte = await ctx.Utilisateurs.FirstOrDefaultAsync(u => u.Id == contenu.Value.Id);

        // L'empreinte change avec le mot de passe : un lien déjà utilisé n'est plus valide.
        if (compte is not { EstActif: true } || EmpreinteDe(compte) != contenu.Value.Empreinte)
            return (false, MessageLienInvalide);

        var empreinte = await Task.Run(() => _hacheur.HashPassword(compte, nouveauMotDePasse!));
        await ModifierCompteAsync(ctx, compte, u =>
        {
            u.PasswordHash = empreinte;
            u.NbEchecsConnexion = 0;
            u.BloqueJusquAu = null;
            u.ModifiePar = u.Id;
        });

        CacheSecurite.Invalider(_cache, compte.Id);

        await _audit.LoggerAsync(compte.Id, compte.Matricule, "REINITIALISATION_MOT_DE_PASSE", ModuleAudit,
            nameof(Utilisateur), compte.Id.ToString(), organismeId: compte.OrganismeId);

        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> ChangerMotDePasseAsync(string? motDePasseActuel, string? nouveauMotDePasse)
    {
        var userId = await _utilisateurCourant.ObtenirUserIdAsync();
        if (userId == Ulid.Empty) return (false, "Session expirée : reconnectez-vous.");

        if (string.IsNullOrEmpty(motDePasseActuel)) return (false, "Saisissez votre mot de passe actuel.");
        if (motDePasseActuel == nouveauMotDePasse) return (false, "Le nouveau mot de passe doit être différent de l'actuel.");

        var erreurPolitique = PolitiqueMotDePasse.Verifier(nouveauMotDePasse, _options.MotDePasse);
        if (erreurPolitique is not null) return (false, erreurPolitique);

        await using var ctx = await _factory.CreateDbContextAsync();
        var compte = await ctx.Utilisateurs.FirstOrDefaultAsync(u => u.Id == userId);
        if (compte is null) return (false, "Compte introuvable.");

        var verification = await Task.Run(() =>
            _hacheur.VerifyHashedPassword(compte, compte.PasswordHash ?? string.Empty, motDePasseActuel));
        if (verification == PasswordVerificationResult.Failed)
        {
            await _audit.LoggerAsync(compte.Id, compte.Matricule, "CHANGEMENT_MOT_DE_PASSE", ModuleAudit,
                nameof(Utilisateur), compte.Id.ToString(), estSucces: false, messageErreur: "Mot de passe actuel incorrect");
            return (false, "Le mot de passe actuel est incorrect.");
        }

        var empreinte = await Task.Run(() => _hacheur.HashPassword(compte, nouveauMotDePasse!));
        await ModifierCompteAsync(ctx, compte, u =>
        {
            u.PasswordHash = empreinte;
            u.ModifiePar = u.Id;
        });

        CacheSecurite.Invalider(_cache, compte.Id);

        await _audit.LoggerAsync(compte.Id, compte.Matricule, "CHANGEMENT_MOT_DE_PASSE", ModuleAudit,
            nameof(Utilisateur), compte.Id.ToString());

        return (true, null);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Outils
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Raison pour laquelle un compte au mot de passe correct ne peut pas se connecter, ou null.</summary>
    private static async Task<string?> MotifRefusAsync(AppDbContext ctx, Utilisateur compte)
    {
        if (!compte.EstActif) return "Ce compte est désactivé. Contactez votre administrateur.";

        if (compte.EstAdministrateurPlateforme) return null;

        if (compte.OrganismeId is null) return "Ce compte n'est rattaché à aucun organisme. Contactez votre administrateur.";

        var statut = await ctx.Organismes.AsNoTracking()
            .Where(o => o.Id == compte.OrganismeId)
            .Select(o => (StatutOrganisme?)o.Statut)
            .FirstOrDefaultAsync();

        return statut == StatutOrganisme.Actif
            ? null
            : "L'accès de votre organisme à la plateforme est suspendu. Contactez l'administrateur de la plateforme.";
    }

    /// <summary>
    /// Paramètres de connexion de l'organisme (cfg.ParametreOrganisme), ou ceux de la
    /// configuration pour un compte plateforme ou un organisme pas encore paramétré.
    /// </summary>
    private async Task<(int NbTentativesMax, int DureeSessionMinutes)> LireParametresAsync(Ulid? organismeId)
    {
        var parDefaut = (Math.Max(1, _options.NbTentativesMax), Math.Max(5, _options.DureeSessionMinutes));
        if (organismeId is null) return parDefaut;

        await using var ctx = await _factory.CreateDbContextAsync();
        ctx.OrganismeCourantId = organismeId.Value;

        var p = await ctx.ParametresOrganisme.AsNoTracking()
            .Select(x => new { x.NbTentativesMax, x.DureeSessionMinutes })
            .FirstOrDefaultAsync();

        return p is null
            ? parDefaut
            : (Math.Max(1, p.NbTentativesMax), Math.Clamp(p.DureeSessionMinutes, 5, 24 * 60));
    }

    /// <summary>
    /// Applique une modification au compte et l'enregistre. Deux tentatives simultanées sur
    /// le même compte se heurtent au RowVersion : on recharge et on rejoue la modification,
    /// pour qu'aucun échec ne soit perdu dans le décompte.
    /// </summary>
    private static async Task ModifierCompteAsync(AppDbContext ctx, Utilisateur compte, Action<Utilisateur> modification)
    {
        for (var essai = 1; ; essai++)
        {
            modification(compte);
            try
            {
                await ctx.SaveChangesAsync();
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (essai < 3)
            {
                foreach (var entree in ex.Entries) await entree.ReloadAsync();
            }
        }
    }

    private static async Task JournaliserAsync(AppDbContext ctx, JournalConnexion ligne, StatutConnexion statut, string? motif)
    {
        ligne.Statut = statut;
        ligne.Motif = motif;
        ctx.JournalConnexions.Add(ligne);
        await ctx.SaveChangesAsync();
    }

    private string ObtenirEmpreinteFictive()
        => _empreinteFictive ??= _hacheur.HashPassword(null!, Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)));

    private (Ulid Id, string Empreinte)? LireJeton(string? jeton)
    {
        if (string.IsNullOrWhiteSpace(jeton)) return null;
        try
        {
            var morceaux = _protecteur.Unprotect(jeton).Split('|');
            return morceaux.Length == 2 && Ulid.TryParse(morceaux[0], out var id) ? (id, morceaux[1]) : null;
        }
        catch (CryptographicException)
        {
            // Jeton falsifié, expiré, ou produit avec une autre clé de protection.
            return null;
        }
    }

    private static string EmpreinteDe(Utilisateur u)
        => Revendications.CalculerEmpreinte(u.Id, u.PasswordHash, u.OrganismeId, u.EstAdministrateurPlateforme);

    private static string MessageBlocage(TimeSpan restant)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(restant.TotalMinutes));
        return $"Trop de tentatives infructueuses : le compte est bloqué. Réessayez dans {minutes} minute(s) " +
               "ou contactez votre administrateur.";
    }

    private string CorpsEmailReinitialisation(string nom, string lien, int minutes)
    {
        var nomApplication = WebUtility.HtmlEncode(_application.Nom);
        return $"""
            <p>Bonjour {WebUtility.HtmlEncode(nom)},</p>
            <p>Une réinitialisation du mot de passe de votre compte {nomApplication} a été demandée.</p>
            <p><a href="{WebUtility.HtmlEncode(lien)}">Choisir un nouveau mot de passe</a></p>
            <p>Ce lien est valable {minutes} minutes et ne peut servir qu'une fois.
               Si vous n'êtes pas à l'origine de cette demande, ignorez ce message : votre mot de passe reste inchangé.</p>
            """;
    }

    private static string? Tronquer(string? valeur, int max)
        => string.IsNullOrEmpty(valeur) || valeur.Length <= max ? valeur : valeur[..max];
}
