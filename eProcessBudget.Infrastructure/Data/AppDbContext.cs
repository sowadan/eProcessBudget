using System.Linq.Expressions;
using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace eProcessBudget.Infrastructure.Data;

/// <summary>
/// Base de données de l'application : tables du socle (utilisateurs, droits, notifications,
/// audit) et des modules métier, décrites dans le cahier technique (chapitre 3).
///
/// Chaque table métier a sa configuration (une classe IEntityTypeConfiguration par entité),
/// regroupées par module dans Data/Configurations/Configurations&lt;Module&gt;.cs. Ce qui vaut
/// pour toutes les tables est appliqué
/// une seule fois ici, à la fin de OnModelCreating (voir « Conventions globales »).
///
/// Ce contexte est consommé via IDbContextFactory et non par injection directe :
/// en Blazor Server, plusieurs composants d'un même circuit peuvent requêter en
/// parallèle, ce qu'une instance unique de DbContext ne supporte pas.
///
/// MULTI-ORGANISME (§ 2.3). Les entités <see cref="IEntiteOrganisme"/> sont filtrées sur
/// <see cref="OrganismeCourantId"/>. Tant qu'il n'est pas renseigné (Ulid.Empty), ces
/// requêtes ne renvoient RIEN : un oubli ne peut pas exposer les données d'un autre
/// organisme. <see cref="ContournerFiltreOrganisme"/> est réservé au module Plateforme
/// (provisionnement d'un organisme) et aux traitements planifiés multi-organismes.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>Organisme de l'utilisateur courant : filtre de lecture et contrôle d'écriture.</summary>
    public Ulid OrganismeCourantId { get; set; }

    /// <summary>Désactive le cloisonnement par organisme. Hors module Plateforme : revue de code obligatoire.</summary>
    public bool ContournerFiltreOrganisme { get; set; }

    /// <summary>Adresse IP du client, reprise dans le journal automatique (renseignée par la fabrique de contexte).</summary>
    public string? AdresseIp { get; set; }

    /// <summary>Identifiant de requête ou de circuit, repris dans le journal automatique.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Journalisation automatique des écritures (§ 2.7) ; désactivée seulement pour l'amorçage en masse.</summary>
    public bool JournalisationActive { get; set; } = true;

    /// <summary>Seule exception à l'ajout seul des journaux : la purge planifiée au-delà de la rétention.</summary>
    internal bool PurgeJournauxEnCours { get; set; }

    // ── Socle : sécurité (schéma auth) ───────────────────────────────────────
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Profil> Profils => Set<Profil>();
    public DbSet<ProfilDroit> ProfilDroits => Set<ProfilDroit>();
    public DbSet<UtilisateurProfil> UtilisateurProfils => Set<UtilisateurProfil>();
    public DbSet<UtilisateurDroit> UtilisateurDroits => Set<UtilisateurDroit>();

    // ── Socle : traçabilité (schéma aud) ─────────────────────────────────────
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ── Socle : notifications (schéma ntf) ───────────────────────────────────
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ParametreNotification> ParametresNotification => Set<ParametreNotification>();

    // ── Plateforme et habilitations (schémas plat, auth) ─────────────────────
    public DbSet<Organisme> Organismes => Set<Organisme>();
    public DbSet<Habilitation> Habilitations => Set<Habilitation>();
    public DbSet<JournalConnexion> JournalConnexions => Set<JournalConnexion>();

    // ── Paramètres généraux (schéma cfg) ─────────────────────────────────────
    public DbSet<ParametreOrganisme> ParametresOrganisme => Set<ParametreOrganisme>();
    public DbSet<Fichier> Fichiers => Set<Fichier>();
    public DbSet<Devise> Devises => Set<Devise>();
    public DbSet<TauxChange> TauxChanges => Set<TauxChange>();
    public DbSet<CycleProgrammation> CyclesProgrammation => Set<CycleProgrammation>();
    public DbSet<Exercice> Exercices => Set<Exercice>();
    public DbSet<Periode> Periodes => Set<Periode>();
    public DbSet<SeuilAlerte> SeuilsAlerte => Set<SeuilAlerte>();

    // ── Référentiels (schéma ref) ────────────────────────────────────────────
    public DbSet<ListeValeur> ListesValeurs => Set<ListeValeur>();
    public DbSet<Axe> Axes => Set<Axe>();
    public DbSet<Structure> Structures => Set<Structure>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<SiteTiers> SitesTiers => Set<SiteTiers>();
    public DbSet<CategorieEmploi> CategoriesEmploi => Set<CategorieEmploi>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<NatureDepense> NaturesDepense => Set<NatureDepense>();
    public DbSet<SourceFinancement> SourcesFinancement => Set<SourceFinancement>();
    public DbSet<CompteComptable> ComptesComptables => Set<CompteComptable>();
    public DbSet<Rubrique> Rubriques => Set<Rubrique>();
    public DbSet<SousRubrique> SousRubriques => Set<SousRubrique>();
    public DbSet<LigneBudgetaire> LignesBudgetaires => Set<LigneBudgetaire>();
    public DbSet<LigneCompte> LignesComptes => Set<LigneCompte>();
    public DbSet<MotifAjustement> MotifsAjustement => Set<MotifAjustement>();
    public DbSet<UniteMesure> UnitesMesure => Set<UniteMesure>();
    public DbSet<Risque> Risques => Set<Risque>();
    public DbSet<TypeTravaux> TypesTravaux => Set<TypeTravaux>();
    public DbSet<Tiers> Tiers => Set<Tiers>();
    public DbSet<NatureRecette> NaturesRecette => Set<NatureRecette>();
    public DbSet<CompteBancaire> ComptesBancaires => Set<CompteBancaire>();

    // ── Budget-programme et performance (schéma prog) ────────────────────────
    public DbSet<Programme> Programmes => Set<Programme>();
    public DbSet<ProgrammeStructure> ProgrammeStructures => Set<ProgrammeStructure>();
    public DbSet<ActionProgramme> Actions => Set<ActionProgramme>();
    public DbSet<Activite> Activites => Set<Activite>();
    public DbSet<CleRepartition> ClesRepartition => Set<CleRepartition>();
    public DbSet<Indicateur> Indicateurs => Set<Indicateur>();
    public DbSet<IndicateurCible> IndicateurCibles => Set<IndicateurCible>();
    public DbSet<IndicateurRealisation> IndicateurRealisations => Set<IndicateurRealisation>();
    public DbSet<CadreLogiqueElement> CadreLogiqueElements => Set<CadreLogiqueElement>();
    public DbSet<CadreLogiqueRisque> CadreLogiqueRisques => Set<CadreLogiqueRisque>();
    public DbSet<ProgrammationFinanciere> ProgrammationsFinancieres => Set<ProgrammationFinanciere>();
    public DbSet<ProgrammationRessource> ProgrammationsRessources => Set<ProgrammationRessource>();
    public DbSet<ProgrammationEmploi> ProgrammationsEmplois => Set<ProgrammationEmploi>();
    public DbSet<RepartitionTemps> RepartitionsTemps => Set<RepartitionTemps>();
    public DbSet<AnalyseQualitative> AnalysesQualitatives => Set<AnalyseQualitative>();

    // ── Cycle budgétaire et exécution des dépenses (schéma bud) ──────────────
    public DbSet<Projet> Projets => Set<Projet>();
    public DbSet<ProjetTravaux> ProjetsTravaux => Set<ProjetTravaux>();
    public DbSet<Contrat> Contrats => Set<Contrat>();
    public DbSet<Dotation> Dotations => Set<Dotation>();
    public DbSet<Ajustement> Ajustements => Set<Ajustement>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<EcheancierCP> EcheanciersCP => Set<EcheancierCP>();
    public DbSet<ValidationDepassement> ValidationsDepassement => Set<ValidationDepassement>();
    public DbSet<Facture> Factures => Set<Facture>();
    public DbSet<Paiement> Paiements => Set<Paiement>();
    public DbSet<RejetPenalite> RejetsPenalites => Set<RejetPenalite>();
    public DbSet<AvancementPhysique> AvancementsPhysiques => Set<AvancementPhysique>();
    public DbSet<BesoinEntretien> BesoinsEntretien => Set<BesoinEntretien>();
    public DbSet<Immobilisation> Immobilisations => Set<Immobilisation>();
    public DbSet<EnveloppeMaitreOuvrage> EnveloppesMaitreOuvrage => Set<EnveloppeMaitreOuvrage>();

    // ── Recettes (schéma rec) ────────────────────────────────────────────────
    public DbSet<PrevisionRecette> PrevisionsRecettes => Set<PrevisionRecette>();
    public DbSet<Encaissement> Encaissements => Set<Encaissement>();
    public DbSet<EmissionRecette> EmissionsRecettes => Set<EmissionRecette>();
    public DbSet<EcheanceRecouvrement> EcheancesRecouvrement => Set<EcheanceRecouvrement>();
    public DbSet<ReportRessource> ReportsRessources => Set<ReportRessource>();

    // ── Historique (schéma hist) ─────────────────────────────────────────────
    public DbSet<ExecutionHistorique> ExecutionsHistoriques => Set<ExecutionHistorique>();

    // ── Vues de calcul (lecture seule) ───────────────────────────────────────
    public DbSet<SituationDotation> SituationsDotations => Set<SituationDotation>();
    public DbSet<ConsommationActivite> ConsommationsActivites => Set<ConsommationActivite>();
    public DbSet<AtteinteIndicateur> AtteintesIndicateurs => Set<AtteinteIndicateur>();
    public DbSet<SituationRecette> SituationsRecettes => Set<SituationRecette>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Contraintes DEFAULT nommées DF_Table_Colonne (§ 2.5) plutôt que par SQL Server.
        b.UseNamedDefaultConstraints();

        ConfigurerSecurite(b);
        ConfigurerTracabilite(b);
        ConfigurerNotifications(b);

        // Tables des modules et vues : Data/Configurations/Configurations<Module>.cs.
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // L'ordre compte : les clés étrangères d'organisme doivent exister avant que
        // le comportement de suppression et les noms d'index ne soient appliqués.
        AppliquerCleOrganisme(b);
        AppliquerFiltresGlobaux(b);
        AppliquerRowVersion(b);
        AppliquerSuppressionSansCascade(b);
        AppliquerGenerationUlid(b);
        AppliquerLongueursParDefaut(b);
        AppliquerHorodatageParDefaut(b);
        AppliquerContraintesEnumerations(b);
        AppliquerNommageIndexUniques(b);
    }

    /// <summary>
    /// Types de colonnes communs (§ 2.4 et § 2.5) :
    ///   - tout Ulid (clé primaire, clé étrangère, champ d'audit, nullable ou non) : BINARY(16) ;
    ///   - tout decimal : DECIMAL(18,2) (montants), sauf précision explicite dans la configuration ;
    ///   - tout DateTime : DATETIME2(3), en UTC. Les dates métier sont des DateOnly (DATE).
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Ulid>()
            .HaveConversion<UlidToBytesConverter>()
            .HaveMaxLength(16)
            .AreFixedLength();

        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Sécurité (schéma auth)
    // ═════════════════════════════════════════════════════════════════════════
    private static void ConfigurerSecurite(ModelBuilder b)
    {
        b.Entity<Utilisateur>(e =>
        {
            e.ToTable("Utilisateur", "auth");
            e.HasIndex(x => x.Matricule).IsUnique();
            // Unicité de l'e-mail (réinitialisation du mot de passe), hors comptes sans e-mail.
            e.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] <> N''");
            e.Property(x => x.Matricule).HasMaxLength(100).IsRequired();
            e.Property(x => x.NomPrenoms).HasMaxLength(300).IsRequired();
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Fonction).HasMaxLength(150);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
        });

        b.Entity<Profil>(e =>
        {
            e.ToTable("Profil", "auth");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Libelle).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
        });

        b.Entity<ProfilDroit>(e =>
        {
            e.ToTable("ProfilDroit", "auth");
            // Un même droit ne peut être listé qu'une fois par profil.
            e.HasIndex(x => new { x.ProfilId, x.TypeDroit }).IsUnique();
            e.HasOne(x => x.Profil)
             .WithMany(p => p.Droits)
             .HasForeignKey(x => x.ProfilId);
        });

        b.Entity<UtilisateurProfil>(e =>
        {
            e.ToTable("UtilisateurProfil", "auth");
            e.HasIndex(x => new { x.UtilisateurId, x.ProfilId });
            e.HasOne(x => x.Utilisateur)
             .WithMany(u => u.UtilisateurProfils)
             .HasForeignKey(x => x.UtilisateurId);
            e.HasOne(x => x.Profil)
             .WithMany(p => p.UtilisateurProfils)
             .HasForeignKey(x => x.ProfilId);
        });

        b.Entity<UtilisateurDroit>(e =>
        {
            e.ToTable("UtilisateurDroit", "auth");
            e.HasIndex(x => new { x.UtilisateurId, x.TypeDroit });
            e.Property(x => x.Justification).HasMaxLength(500);
            e.HasOne(x => x.Utilisateur)
             .WithMany(u => u.UtilisateurDroits)
             .HasForeignKey(x => x.UtilisateurId);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Traçabilité (schéma aud)
    // ═════════════════════════════════════════════════════════════════════════
    private static void ConfigurerTracabilite(ModelBuilder b)
    {
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("JournalAudit", "aud");
            // Index principal de la page Audit : filtrage par date décroissante.
            e.HasIndex(x => x.DateAction);
            e.HasIndex(x => new { x.Module, x.DateAction });
            e.HasIndex(x => new { x.OrganismeId, x.DateAction });
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.Module).HasMaxLength(100).IsRequired();
            e.Property(x => x.Entite).HasMaxLength(150).IsRequired();
            e.Property(x => x.EntiteId).HasMaxLength(100);
            e.Property(x => x.UtilisateurMatricule).HasMaxLength(100);
            e.Property(x => x.AdresseIP).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(400);
            e.Property(x => x.Commentaire).HasMaxLength(1000);
            e.Property(x => x.MessageErreur).HasMaxLength(2000);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            // Valeurs avant / après en JSON : NVARCHAR(MAX).
            e.Property(x => x.AncienneValeur).HasColumnType("nvarchar(max)");
            e.Property(x => x.NouvelleValeur).HasColumnType("nvarchar(max)");
            e.Property(x => x.DateAction).HasDefaultValueSql("SYSUTCDATETIME()");
            // UtilisateurId volontairement sans clé étrangère : le journal reste immuable.
            e.HasOne<Organisme>().WithMany().HasForeignKey(x => x.OrganismeId);
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Notifications (schéma ntf)
    // ═════════════════════════════════════════════════════════════════════════
    private static void ConfigurerNotifications(ModelBuilder b)
    {
        b.Entity<Notification>(e =>
        {
            e.ToTable("Notification", "ntf");
            // Index qui sert la cloche : « mes notifications non lues, les plus récentes ».
            e.HasIndex(x => new { x.UtilisateurId, x.EstLue, x.DateCreation });
            e.Property(x => x.Titre).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Lien).HasMaxLength(400);
            e.Property(x => x.CleEntite).HasMaxLength(100);
            e.HasOne(x => x.Utilisateur)
             .WithMany(u => u.Notifications)
             .HasForeignKey(x => x.UtilisateurId);
            e.HasOne<Organisme>().WithMany().HasForeignKey(x => x.OrganismeId);
        });

        b.Entity<ParametreNotification>(e =>
        {
            e.ToTable("ParametreNotification", "ntf");
            e.HasIndex(x => x.Type).IsUnique();
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Conventions globales
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Clé étrangère OrganismeId → plat.Organisme sur toute table rattachée à un organisme.</summary>
    private static void AppliquerCleOrganisme(ModelBuilder b)
    {
        foreach (var type in TablesDe<IEntiteOrganisme>(b))
        {
            b.Entity(type.ClrType)
             .HasOne(typeof(Organisme))
             .WithMany()
             .HasForeignKey(nameof(IEntiteOrganisme.OrganismeId));
        }
    }

    /// <summary>
    /// Filtres appliqués à toutes les requêtes (§ 2.3) :
    ///   - suppression logique : les lignes EstSupprime = 1 sont invisibles ;
    ///   - organisme : seules les lignes de l'organisme courant sont visibles.
    /// Pour les contourner ponctuellement : .IgnoreQueryFilters() (Plateforme, revue de code).
    ///
    /// Le filtre référence l'instance du contexte : EF Core la remplace à chaque requête
    /// par le contexte qui l'exécute, OrganismeCourantId est donc lu au moment de la requête.
    /// </summary>
    private void AppliquerFiltresGlobaux(ModelBuilder b)
    {
        var contexte = Expression.Constant(this);

        foreach (var type in b.Model.GetEntityTypes().Where(t => t.BaseType is null).ToList())
        {
            var clr = type.ClrType;
            var e = Expression.Parameter(clr, "e");
            Expression? corps = null;

            if (typeof(BaseEntity).IsAssignableFrom(clr))
                corps = Expression.Not(Expression.Property(e, nameof(BaseEntity.EstSupprime)));

            if (typeof(IEntiteOrganisme).IsAssignableFrom(clr))
            {
                var memeOrganisme = Expression.OrElse(
                    Expression.Property(contexte, nameof(ContournerFiltreOrganisme)),
                    Expression.Equal(
                        Expression.Property(e, nameof(IEntiteOrganisme.OrganismeId)),
                        Expression.Property(contexte, nameof(OrganismeCourantId))));

                corps = corps is null ? memeOrganisme : Expression.AndAlso(corps, memeOrganisme);
            }

            if (corps is not null)
                b.Entity(clr).HasQueryFilter(Expression.Lambda(corps, e));
        }
    }

    /// <summary>Concurrence optimiste : RowVersion sur toutes les tables complètes.</summary>
    private static void AppliquerRowVersion(ModelBuilder b)
    {
        foreach (var type in TablesDe<BaseEntity>(b))
            b.Entity(type.ClrType).Property(nameof(BaseEntity.RowVersion)).IsRowVersion();
    }

    /// <summary>Aucune suppression en cascade : toutes les clés étrangères en NO ACTION (§ 2.5).</summary>
    private static void AppliquerSuppressionSansCascade(ModelBuilder b)
    {
        foreach (var cle in b.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            cle.DeleteBehavior = DeleteBehavior.NoAction;
    }

    /// <summary>
    /// Les clés primaires Ulid sont générées côté application, à l'ajout : la base ne
    /// génère jamais d'identifiant. Exception : une clé primaire qui est aussi clé
    /// étrangère (relation 1-1, ex. ProjetTravaux) reprend l'Id de son principal.
    /// </summary>
    private static void AppliquerGenerationUlid(ModelBuilder b)
    {
        foreach (var type in b.Model.GetEntityTypes())
        {
            var cle = type.FindPrimaryKey();
            if (cle is not { Properties.Count: 1 } || cle.Properties[0].ClrType != typeof(Ulid)) continue;
            if (cle.Properties[0].IsForeignKey()) continue;

            b.Entity(type.ClrType).Property(cle.Properties[0].Name)
             .HasValueGenerator<UlidValueGenerator>()
             .ValueGeneratedOnAdd();
        }
    }

    /// <summary>
    /// Borne toute chaîne non configurée à 400 caractères, pour éviter les colonnes
    /// nvarchar(max) créées par inadvertance (coûteuses et non indexables). Un texte long
    /// se déclare explicitement (HasColumnType("nvarchar(max)") ou EnTexteLong()).
    /// </summary>
    private static void AppliquerLongueursParDefaut(ModelBuilder b)
    {
        foreach (var propriete in b.Model.GetEntityTypes()
                                         .SelectMany(t => t.GetProperties())
                                         .Where(p => p.ClrType == typeof(string)
                                                  && p.GetMaxLength() is null
                                                  && p.GetColumnType() is null))
        {
            propriete.SetMaxLength(400);
        }
    }

    /// <summary>DateCreation : valeur par défaut SYSUTCDATETIME() côté base (DF_).</summary>
    private static void AppliquerHorodatageParDefaut(ModelBuilder b)
    {
        foreach (var type in b.Model.GetEntityTypes().Where(t => t.FindPrimaryKey() is not null))
        {
            var dateCreation = type.FindProperty(nameof(BaseEntity.DateCreation));
            if (dateCreation?.ClrType == typeof(DateTime))
                dateCreation.SetDefaultValueSql("SYSUTCDATETIME()");
        }
    }

    /// <summary>
    /// Énumérations métier stockées en TINYINT : une contrainte CHECK (CK_Table_Colonne)
    /// restreint la colonne aux valeurs de l'énumération C#. Ajouter une valeur à une
    /// énumération produit donc une migration qui élargit la contrainte.
    /// </summary>
    private static void AppliquerContraintesEnumerations(ModelBuilder b)
    {
        foreach (var type in b.Model.GetEntityTypes().Where(t => t.FindPrimaryKey() is not null && t.GetTableName() is not null))
        {
            foreach (var propriete in type.GetDeclaredProperties())
            {
                var clr = Nullable.GetUnderlyingType(propriete.ClrType) ?? propriete.ClrType;
                if (!clr.IsEnum || Enum.GetUnderlyingType(clr) != typeof(byte)) continue;

                var colonne = propriete.GetColumnName();
                var valeurs = string.Join(", ", Enum.GetValues(clr).Cast<object>().Select(v => Convert.ToByte(v)));
                type.AddCheckConstraint($"CK_{type.GetTableName()}_{colonne}", $"[{colonne}] IN ({valeurs})");
            }
        }
    }

    /// <summary>
    /// Index uniques nommés UX_Table_Colonnes (§ 2.5). Sur les tables métier à suppression
    /// logique, l'unicité ne porte que sur les lignes actives (filtre EstSupprime = 0) :
    /// un code supprimé peut être réutilisé.
    /// </summary>
    private static void AppliquerNommageIndexUniques(ModelBuilder b)
    {
        foreach (var type in b.Model.GetEntityTypes().Where(t => t.GetTableName() is not null))
        {
            var filtrerSupprimes = typeof(EntiteOrganisme).IsAssignableFrom(type.ClrType);

            foreach (var index in type.GetIndexes().Where(i => i.IsUnique))
            {
                index.SetDatabaseName($"UX_{type.GetTableName()}_{string.Join("_", index.Properties.Select(p => p.Name))}");
                if (filtrerSupprimes && index.GetFilter() is null)
                    index.SetFilter("[EstSupprime] = 0");
            }
        }
    }

    /// <summary>Types d'entités mappés sur une table (avec clé) dont le type CLR dérive de <typeparamref name="T"/>.</summary>
    private static List<IMutableEntityType> TablesDe<T>(ModelBuilder b)
        => b.Model.GetEntityTypes()
                  .Where(t => t.FindPrimaryKey() is not null && typeof(T).IsAssignableFrom(t.ClrType))
                  .ToList();

    // ═════════════════════════════════════════════════════════════════════════
    //  Enregistrement
    // ═════════════════════════════════════════════════════════════════════════

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PreparerEnregistrement();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PreparerEnregistrement();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Avant chaque enregistrement :
    ///   - renseigne DateModification, pour que la traçabilité ne dépende pas de l'appelant ;
    ///   - renseigne OrganismeId à la création et refuse toute écriture sur un autre
    ///     organisme que l'organisme courant (§ 2.3) ;
    ///   - refuse toute modification des journaux (ajout seul) et journalise les écritures (§ 2.7).
    /// </summary>
    private void PreparerEnregistrement()
    {
        // Enregistrement retenté après un échec (collision de numéro, concurrence) : les lignes
        // de journal produites par la tentative précédente sont encore en attente. On les retire,
        // elles seront décrites à nouveau ci-dessous, sans doublon.
        foreach (var ligne in _journalAutomatiqueEnAttente)
            if (Entry(ligne).State == EntityState.Added) Entry(ligne).State = EntityState.Detached;
        _journalAutomatiqueEnAttente = [];

        var entrees = ChangeTracker.Entries().ToList();
        foreach (var entree in entrees)
        {
            if (!PurgeJournauxEnCours) JournalisationAutomatique.VerifierAjoutSeul(entree);

            if (entree.State == EntityState.Modified && entree.Entity is BaseEntity entite)
            {
                entite.DateModification = DateTime.UtcNow;
                if (entite.EstSupprime && entite.DateSuppression is null) entite.DateSuppression = DateTime.UtcNow;
            }

            if (entree.Entity is not IEntiteOrganisme rattachee) continue;

            switch (entree.State)
            {
                case EntityState.Added:
                    if (rattachee.OrganismeId == Ulid.Empty && !ContournerFiltreOrganisme)
                        rattachee.OrganismeId = OrganismeCourantId;

                    if (rattachee.OrganismeId == Ulid.Empty)
                        throw new InvalidOperationException(
                            $"{entree.Metadata.ClrType.Name} : aucun organisme. Renseignez " +
                            $"{nameof(OrganismeCourantId)} sur le contexte avant d'enregistrer.");

                    if (!ContournerFiltreOrganisme && rattachee.OrganismeId != OrganismeCourantId)
                        throw new InvalidOperationException(
                            $"{entree.Metadata.ClrType.Name} : création refusée pour un autre organisme que l'organisme courant.");
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var origine = (Ulid)entree.Property(nameof(IEntiteOrganisme.OrganismeId)).OriginalValue!;
                    if (!ContournerFiltreOrganisme && origine != OrganismeCourantId)
                        throw new InvalidOperationException(
                            $"{entree.Metadata.ClrType.Name} : modification refusée sur un enregistrement d'un autre organisme.");
                    if (rattachee.OrganismeId != origine)
                        throw new InvalidOperationException(
                            $"{entree.Metadata.ClrType.Name} : l'organisme d'un enregistrement ne peut pas changer.");
                    break;
            }
        }

        // Journal automatique, enregistré dans la même transaction que les écritures qu'il décrit.
        if (JournalisationActive)
        {
            _journalAutomatiqueEnAttente = JournalisationAutomatique.Decrire(this, entrees);
            AuditLogs.AddRange(_journalAutomatiqueEnAttente);
        }
    }

    /// <summary>Lignes de journal automatique ajoutées au dernier enregistrement (retirées s'il est retenté).</summary>
    private List<AuditLog> _journalAutomatiqueEnAttente = [];
}
