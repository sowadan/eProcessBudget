using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.BudgetProgramme;
using eProcessBudget.Infrastructure.Services.Consommation;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Notifications;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Infrastructure.Services.Rapports;
using eProcessBudget.Infrastructure.Services.Recettes;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eProcessBudget.Infrastructure;

/// <summary>
/// POINT DE CÂBLAGE UNIQUE du socle.
///
/// Program.cs appelle AddSocleApplicatif(configuration) et n'a plus à savoir quelles
/// implémentations existent : base SQL Server, sécurité, notifications, modules.
///
/// L'authentification par cookie elle-même (schéma, options du cookie, état Blazor) est
/// configurée dans Program.cs : elle relève de l'hôte web.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSocleApplicatif(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. Options typées, liées aux sections de appsettings.json ─────────
        services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.SectionName));
        services.Configure<PersistanceOptions>(configuration.GetSection(PersistanceOptions.SectionName));
        services.Configure<SecuriteOptions>(configuration.GetSection(SecuriteOptions.SectionName));
        services.Configure<AmorcageOptions>(configuration.GetSection(AmorcageOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        // IHttpContextAccessor est enregistré par Program.cs (côté Web).
        services.AddMemoryCache();
        services.AddDataProtection();

        // ── 2. Accès aux données (SQL Server) ────────────────────────────────
        var chaine = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(chaine))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection est vide : renseignez la chaîne de connexion " +
                "à la base SQL Server (appsettings, user-secrets ou variable d'environnement).");

        // Factory et non AddDbContext : en Blazor Server, plusieurs composants d'un même
        // circuit peuvent requêter en parallèle, ce qu'une instance partagée ne supporte pas.
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(chaine, sql =>
            {
                sql.EnableRetryOnFailure(3);
                sql.CommandTimeout(30);
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
                // Évite le produit cartésien quand plusieurs collections sont incluses.
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            }));

        AjouterCloisonnementOrganisme(services);

        // ── 3. Sécurité : identité, authentification, droits ─────────────────
        services.AddScoped<IPrincipalCourant, PrincipalCourant>();
        services.AddScoped<IUserContextService, UserContextService>();
        services.AddSingleton<IPasswordHasher<Utilisateur>, HacheurMotDePasseBCrypt>();
        services.AddScoped<IAuthentificationService, AuthentificationService>();
        services.AddScoped<IJournalConnexionService, JournalConnexionService>();
        services.AddScoped<IUtilisateurStore, UtilisateurStoreEf>();
        services.AddScoped<IDroitService, DroitServiceEf>();
        services.AddScoped<IProfilService, ProfilServiceEf>();
        services.AddScoped<IUtilisateurService, UtilisateurServiceEf>();
        services.AddScoped<IContexteConnexion, ContexteConnexion>();
        services.AddScoped<IAuditService, AuditServiceEf>();
        services.AddScoped<IJournalAuditService, JournalAuditService>();
        services.AddHostedService<TraitementPurgeJournaux>();

        // ── 4. Module Plateforme et habilitations ────────────────────────────
        services.AddScoped<IOrganismeService, OrganismeService>();
        services.AddScoped<IHabilitationService, HabilitationService>();
        services.AddScoped<IControlePerimetre, ControlePerimetre>();

        // ── 5. Module Paramétrage ────────────────────────────────────────────
        // Un service générique par référentiel : IReferentielService<Structure>, <Axe>...
        services.AddScoped<AccesParametrage>();
        services.AddScoped(typeof(IReferentielService<>), typeof(ReferentielService<>));
        services.AddScoped<IParametresOrganismeService, ParametresOrganismeService>();
        services.AddScoped<IExerciceService, ExerciceService>();
        services.AddScoped<ICycleProgrammationService, CycleProgrammationService>();
        services.AddScoped<ISeuilAlerteService, SeuilAlerteService>();
        services.AddScoped<ITauxChangeService, TauxChangeService>();
        services.AddScoped<INomenclatureService, NomenclatureService>();
        services.AddScoped<IHistoriqueService, HistoriqueService>();

        // ── 6. Module Budget-programme et performance ────────────────────────
        // Programmes, actions, activités, indicateurs : IReferentielService<T> (ci-dessus).
        services.AddScoped<AccesBudgetProgramme>();
        services.AddScoped<IProgrammeService, ProgrammeService>();
        services.AddScoped<IIndicateurService, IndicateurService>();
        services.AddScoped<ICadreLogiqueService, CadreLogiqueService>();
        services.AddScoped<IProgrammationService, ProgrammationService>();
        services.AddScoped<IRepartitionService, RepartitionService>();
        services.AddScoped<IAnalyseService, AnalyseService>();

        // ── 7. Cycle budgétaire commun (exécution) ───────────────────────────
        services.AddScoped<AccesExecution>();
        services.AddScoped<ISituationService, SituationService>();
        services.AddScoped<IDotationService, DotationService>();
        services.AddScoped<IAjustementService, AjustementService>();
        services.AddScoped<IEngagementService, EngagementService>();
        services.AddScoped<IDepassementService, DepassementService>();
        services.AddScoped<IPaiementService, PaiementService>();
        services.AddScoped<IClotureService, ClotureService>();
        services.AddScoped<IAlerteService, AlerteService>();
        services.AddHostedService<TraitementAlertesNocturne>();

        // ── 7 bis. Axes de dépenses (Entretien, Investissements, Fonctionnement, Appuis)
        services.AddScoped<AccesAxes>();
        services.AddScoped<IProjetService, ProjetService>();
        services.AddScoped<IContratService, ContratService>();
        services.AddScoped<IAvancementService, AvancementService>();
        services.AddScoped<IBesoinEntretienService, BesoinEntretienService>();
        services.AddScoped<IImmobilisationService, ImmobilisationService>();
        services.AddScoped<ISyntheseAxeService, SyntheseAxeService>();
        services.AddHostedService<TraitementAppuisNocturne>();

        // ── 7 ter. Recettes ──────────────────────────────────────────────────
        services.AddScoped<AccesRecettes>();
        services.AddScoped<IPrevisionRecetteService, PrevisionRecetteService>();
        services.AddScoped<IEncaissementService, EncaissementService>();
        services.AddScoped<IEmissionRecetteService, EmissionRecetteService>();
        services.AddScoped<ICompteSurChiffresService, CompteSurChiffresService>();
        services.AddScoped<IReportRessourceService, ReportRessourceService>();
        services.AddScoped<ISyntheseRecetteService, SyntheseRecetteService>();
        services.AddHostedService<TraitementRecouvrementNocturne>();

        // ── 7 quater. Consommation budgétaire et tableau de bord ─────────────
        services.AddScoped<AccesConsommation>();
        services.AddScoped<IConsommationService, ConsommationService>();
        services.AddScoped<ITableauDeBordService, TableauDeBordService>();

        // ── 7 quinquies. Rapports et exportation ─────────────────────────────
        // Licence QuestPDF déclarée par configuration (cahier technique § 2.1 : à vérifier).
        var rapports = configuration.GetSection(RapportsOptions.SectionName).Get<RapportsOptions>() ?? new RapportsOptions();
        QuestPDF.Settings.License = Enum.TryParse<QuestPDF.Infrastructure.LicenseType>(rapports.LicenceQuestPdf, ignoreCase: true, out var licence)
            ? licence
            : throw new InvalidOperationException($"Rapports:LicenceQuestPdf : « {rapports.LicenceQuestPdf} » n'est pas une licence QuestPDF (Community, Professional, Enterprise).");
        services.Configure<RapportsOptions>(configuration.GetSection(RapportsOptions.SectionName));
        services.AddScoped<GenerateurRapports>();
        services.AddScoped<IRapportService, RapportService>();
        services.AddSingleton<DepotRapports>();

        // ── 8. Notifications ─────────────────────────────────────────────────
        // Le dispatcher est un singleton : c'est le point de rendez-vous entre le service
        // qui publie et les cloches abonnées, quels que soient leurs circuits.
        services.AddSingleton<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationStore, NotificationStoreEf>();
        services.AddScoped<IParametreNotificationService, ParametreNotificationServiceEf>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddSingleton<IEmailQueue, EmailQueue>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddHostedService<EmailQueueProcessor>();

        return services;
    }

    /// <summary>
    /// Remplace la fabrique EF Core par <see cref="FabriqueContexteOrganisme"/>, qui renseigne
    /// l'organisme de l'utilisateur connecté sur chaque contexte créé (§ 2.3). La fabrique
    /// d'origine reste accessible en service à clé <see cref="FabriqueContexteOrganisme.CleSansOrganisme"/>.
    /// </summary>
    private static void AjouterCloisonnementOrganisme(IServiceCollection services)
    {
        var origine = services.Single(d => d.ServiceType == typeof(IDbContextFactory<AppDbContext>) && !d.IsKeyedService);
        services.Remove(origine);

        services.Add(origine.ImplementationType is not null
            ? new ServiceDescriptor(origine.ServiceType, FabriqueContexteOrganisme.CleSansOrganisme,
                                    origine.ImplementationType, origine.Lifetime)
            : new ServiceDescriptor(origine.ServiceType, FabriqueContexteOrganisme.CleSansOrganisme,
                                    (sp, _) => origine.ImplementationFactory!(sp), origine.Lifetime));

        services.AddScoped<IDbContextFactory<AppDbContext>, FabriqueContexteOrganisme>();
    }
}
