using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eProcessBudget.Infrastructure.Data;

/// <summary>
/// Données de référence créées au démarrage quand Persistance:MigrerAuDemarrage = true.
///
/// Tout est IDEMPOTENT : la méthode peut s'exécuter à chaque démarrage sans rien dupliquer.
/// C'est une exigence, pas une commodité - elle tourne à chaque lancement en développement.
///
/// Ce que fait le seed :
///   1. les profils livrés (cahier technique § 3.7 et § 3.1.3) : créés s'ils manquent, avec
///      leurs droits par défaut ; ensuite, ils appartiennent aux administrateurs (le seed ne
///      les modifie plus), sauf les deux profils d'administration complétés à chaque démarrage ;
///   2. une ligne de paramétrage par type de notification ;
///   3. le compte d'amorçage (section Amorcage) : Administrateur plateforme, créé si la table
///      des utilisateurs est vide et qu'un mot de passe est fourni.
/// </summary>
public static class SeedService
{
    /// <summary>Administrateur d'un organisme : tous les droits de l'organisme.</summary>
    public const string CodeProfilAdministrateur = "ADMIN";
    public const string CodeProfilAdministrateurPlateforme = "ADMIN_PLATEFORME";
    public const string CodeProfilConsultation = "CONSULTATION";

    /// <summary>Profil livré : code, libellé, description et droits par défaut.</summary>
    private sealed record ProfilLivre(string Code, string Libelle, string Description, IReadOnlyCollection<TypeDroit> Droits);

    /// <summary>Consultation de toutes les données budgétaires, sans saisie.</summary>
    private static readonly TypeDroit[] LectureBudgetaire =
    [
        TypeDroit.ParametrageLire, TypeDroit.BudgetProgrammeLire, TypeDroit.ExecutionLire,
        TypeDroit.EntretienLire, TypeDroit.InvestissementLire, TypeDroit.FonctionnementLire, TypeDroit.AppuisLire,
        TypeDroit.RecettesLire, TypeDroit.TableauDeBordLire, TypeDroit.ConsommationLire
    ];

    /// <summary>
    /// Profils du cahier fonctionnel (§ 3.1.3). Les droits de SAISIE qu'ils portent ne
    /// s'exercent que sur les périmètres des habilitations actives de chaque utilisateur.
    /// </summary>
    private static IEnumerable<ProfilLivre> ProfilsLivres()
    {
        var tousDroitsOrganisme = DroitHelper.FiltrerSelonNiveau(Enum.GetValues<TypeDroit>().Distinct(), false).ToList();

        yield return new(CodeProfilAdministrateur, "Administrateur",
            "Accès total à l'organisme : utilisateurs, paramétrage, toutes les fonctionnalités. Valide les droits de saisie au titre de l'administration du SI.",
            tousDroitsOrganisme);

        yield return new(CodeProfilAdministrateurPlateforme, "Administrateur plateforme",
            "Création, activation et suspension des organismes ; administration technique. Aucun accès aux données budgétaires.",
            DroitHelper.AdmisPlateforme);

        yield return new("DIRECTION", "Direction",
            "Définit les axes, programmes, objectifs, indicateurs et structures responsables. Consultation de tous les tableaux de bord et rapports.",
            [.. LectureBudgetaire, TypeDroit.BudgetProgrammeDefinir, TypeDroit.RapportExporter]);

        yield return new("RESP_FINANCIER", "Responsable du service financier et comptable",
            "Saisie centralisée des données des programmes (actions, activités, réalisations des indicateurs). Valide les droits de saisie au titre du service financier.",
            [.. LectureBudgetaire, TypeDroit.BudgetProgrammeDefinir, TypeDroit.IndicateurSaisirRealisation,
             TypeDroit.HabilitationDemander, TypeDroit.HabilitationValiderFinancier,
             TypeDroit.UtilisateurConsultation, TypeDroit.RapportExporter]);

        yield return new("RESP_BUDGET", "Responsable Budget",
            "Coûts des programmes, prévisions et ajustements, programmation AE/CP, prévisions de recettes ; valide les engagements et les dépassements de dotation.",
            [.. LectureBudgetaire, TypeDroit.ProgrammationSaisir, TypeDroit.DotationSaisir,
             TypeDroit.AjustementSaisir, TypeDroit.AjustementValider, TypeDroit.EngagementValider,
             TypeDroit.DepassementValider, TypeDroit.PrevisionRecetteSaisir, TypeDroit.RapportExporter]);

        yield return new("RESP_PROGRAMME", "Responsable de programme",
            "Consultation de son programme ; transmet les données au service financier, sans saisie directe.",
            [TypeDroit.BudgetProgrammeLire, TypeDroit.ExecutionLire, TypeDroit.TableauDeBordLire, TypeDroit.ConsommationLire]);

        yield return new("COMPTABLE", "Comptable",
            "Saisie des réalisations, paiements et écritures sur les axes assignés par habilitation ; lecture seule ailleurs.",
            [.. LectureBudgetaire, TypeDroit.EngagementSaisir, TypeDroit.PaiementSaisir,
             TypeDroit.EntretienSaisir, TypeDroit.InvestissementSaisir, TypeDroit.FonctionnementSaisir,
             TypeDroit.AppuisSaisir, TypeDroit.EncaissementSaisir]);

        yield return new("AUDITEUR", "Auditeur",
            "Consultation des journaux d'audit, de l'historique des modifications et des rapports de traçabilité.",
            [.. LectureBudgetaire, TypeDroit.AuditConsultation, TypeDroit.AuditExportation, TypeDroit.RapportExporter]);

        yield return new(CodeProfilConsultation, "Consultation",
            "Accès en lecture seule, sans droit de modification.",
            [TypeDroit.UtilisateurConsultation, TypeDroit.ProfilConsultation]);
    }

    public static async Task InitialiserAsync(
        AppDbContext ctx, AmorcageOptions amorcage, SecuriteOptions securite,
        IPasswordHasher<Utilisateur> hacheur, ILogger logger)
    {
        await InitialiserProfilsAsync(ctx, logger);
        await InitialiserParametresNotificationAsync(ctx);
        await InitialiserCompteAmorcageAsync(ctx, amorcage, securite, hacheur, logger);
    }

    // ═════════════════════════════════════════════════════════════════════════
    private static async Task InitialiserProfilsAsync(AppDbContext ctx, ILogger logger)
    {
        var existants = await ctx.Profils.Include(p => p.Droits).ToDictionaryAsync(p => p.Code);

        foreach (var livre in ProfilsLivres())
        {
            if (!existants.TryGetValue(livre.Code, out var profil))
            {
                ctx.Profils.Add(new Profil
                {
                    Code        = livre.Code,
                    Libelle     = livre.Libelle,
                    Description = livre.Description,
                    EstSysteme  = true,
                    EstActif    = true,
                    Droits      = livre.Droits.Select(d => new ProfilDroit { TypeDroit = d }).ToList()
                });
                logger.LogInformation("Profil {Code} créé.", livre.Code);
                continue;
            }

            // Profils d'administration : tout droit ajouté à l'énumération leur est accordé
            // automatiquement. On ajoute sans jamais retirer.
            if (livre.Code is CodeProfilAdministrateur or CodeProfilAdministrateurPlateforme)
            {
                var presents = profil.Droits.Select(d => d.TypeDroit).ToHashSet();
                foreach (var droit in livre.Droits.Where(d => !presents.Contains(d)))
                    ctx.ProfilDroits.Add(new ProfilDroit { ProfilId = profil.Id, TypeDroit = droit });
            }
        }

        await ctx.SaveChangesAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    private static async Task InitialiserParametresNotificationAsync(AppDbContext ctx)
    {
        var existants = await ctx.ParametresNotification.Select(p => p.Type).ToListAsync();
        var manquants = Enum.GetValues<TypeNotification>().Except(existants).ToList();
        if (manquants.Count == 0) return;

        foreach (var type in manquants)
        {
            ctx.ParametresNotification.Add(new ParametreNotification
            {
                Type              = type,
                CanalInterneActif = true,
                // Email désactivé au départ : on n'envoie rien tant que le relais SMTP
                // n'a pas été vérifié par l'exploitant.
                CanalEmailActif   = false
            });
        }

        await ctx.SaveChangesAsync();
    }

    // ═════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Base neuve : crée le compte d'amorçage, Administrateur plateforme, pour pouvoir créer
    /// les organismes et leurs administrateurs.
    ///
    /// Base existante dont le compte d'amorçage n'a pas encore de mot de passe (base créée
    /// avant le module Authentification) : lui donne le mot de passe fourni ; un compte sans
    /// organisme devient Administrateur plateforme.
    ///
    /// Ne fait rien d'autre : la configuration ne doit pas pouvoir élever des droits ni
    /// changer le mot de passe d'un compte déjà en service.
    /// </summary>
    private static async Task InitialiserCompteAmorcageAsync(
        AppDbContext ctx, AmorcageOptions options, SecuriteOptions securite,
        IPasswordHasher<Utilisateur> hacheur, ILogger logger)
    {
        var login = (options.Login ?? string.Empty).Trim();
        if (login.Length == 0) return;

        var baseVide = !await ctx.Utilisateurs.IgnoreQueryFilters().AnyAsync();
        var compte = baseVide ? null : await ctx.Utilisateurs.Include(u => u.UtilisateurProfils)
                                                .FirstOrDefaultAsync(u => u.Matricule == login);

        if (!baseVide && compte is not { PasswordHash: null }) return;

        if (string.IsNullOrEmpty(options.MotDePasse))
        {
            logger.LogWarning(
                "Compte d'amorçage « {Login} » sans mot de passe : fournissez Amorcage:MotDePasse " +
                "(dotnet user-secrets en développement, variable Amorcage__MotDePasse en production) " +
                "puis redémarrez. Aucun compte n'est connectable en attendant.", login);
            return;
        }

        var erreurPolitique = Services.Securite.PolitiqueMotDePasse.Verifier(options.MotDePasse, securite.MotDePasse);
        if (erreurPolitique is not null)
        {
            logger.LogError("Amorcage:MotDePasse refusé : {Erreur} Le compte d'amorçage n'a pas été initialisé.", erreurPolitique);
            return;
        }

        var profilPlateforme = await ctx.Profils.FirstAsync(p => p.Code == CodeProfilAdministrateurPlateforme);

        if (compte is null)
        {
            compte = new Utilisateur
            {
                Matricule  = login,
                NomPrenoms = string.IsNullOrWhiteSpace(options.NomPrenoms) ? login : options.NomPrenoms.Trim(),
                Email      = (options.Email ?? string.Empty).Trim(),
                EstActif   = true
            };
            ctx.Utilisateurs.Add(compte);
        }

        if (compte.OrganismeId is null)
        {
            compte.EstAdministrateurPlateforme = true;
            if (compte.UtilisateurProfils.All(up => up.ProfilId != profilPlateforme.Id))
                compte.UtilisateurProfils.Add(new UtilisateurProfil { ProfilId = profilPlateforme.Id });
        }

        compte.PasswordHash = await Task.Run(() => hacheur.HashPassword(compte, options.MotDePasse));
        await ctx.SaveChangesAsync();

        logger.LogInformation("Compte d'amorçage {Login} prêt (Administrateur plateforme : {Plateforme}).",
            login, compte.EstAdministrateurPlateforme);
    }
}
