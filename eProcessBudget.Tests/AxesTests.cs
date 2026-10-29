using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace eProcessBudget.Tests;

/// <summary>Tests clés des axes de dépenses (cahier technique § 4.5).</summary>
public class AxesTests
{
    // ═════ Formules ═══════════════════════════════════════════════════════════

    [Fact]
    public void Formules_propres_aux_axes()
    {
        Assert.Equal(62.5m, CalculsAxes.TauxAvancementKm(25, 40));          // km réalisés / km prévus
        Assert.Null(CalculsAxes.TauxAvancementKm(10, null));                 // « n.s. » sans km prévus
        Assert.Null(CalculsAxes.TauxAvancementKm(10, 0));
        Assert.Equal(300, CalculsAxes.SoldeAppui(1_000, 700));               // prévu − versé
        Assert.Equal(1_150, CalculsAxes.MontantContrat(1_000, 150));         // initial + avenants

        // Une clé 60 / 40 ventile tous les montants ; les parts retombent sur le total.
        var m = new MontantsDotation(1_000, 100, 0, 800, 0, 500, 500, 0);
        var a = CalculsAxes.Repartir(m, 60);
        var b = CalculsAxes.Repartir(m, 40);
        Assert.Equal(660, a.PrevisionAjustee);
        Assert.Equal(480, a.AETotal);
        Assert.Equal(m.DotationTotale, a.DotationTotale + b.DotationTotale);
        Assert.Equal(m.PaiementsTotal, a.PaiementsTotal + b.PaiementsTotal);
        Assert.Equal(33.33m, CalculsAxes.Part(100, 33.33m));
    }

    [Fact]
    public void Chaque_axe_a_ses_droits_et_la_saisie_donne_la_lecture()
    {
        Assert.Equal(4, AxesDepense.Tous.Count);
        Assert.Contains(TypeDroit.EntretienSaisir, AxesDepense.Entretien.Lecture);
        Assert.Equal(AxesDepense.Appuis, AxesDepense.DeCode("APPUIS"));
        Assert.Null(AxesDepense.DeCode("RECETTES"));
        Assert.Equal([TypeContrat.ConventionAppui], LibellesAxes.TypesContrat("APPUIS"));
    }

    // ═════ Services sur base en mémoire ═══════════════════════════════════════

    private sealed record Scene(Environnement Env, Ulid Organisme, Utilisateur Gestionnaire, Ulid ExerciceId,
                                Dictionary<string, LigneBudgetaire> Lignes, NotificationsFactices Notifications)
    {
        public AccesAxes Acces() => new(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme)));
        public AccesExecution AccesExecution() => new(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme)));
        public ProjetService Projets() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public ContratService Contrats() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public AvancementService Avancements() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public ImmobilisationService Immobilisations() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public SyntheseAxeService Synthese() => new(Env.Fabrique(Organisme), Acces());
        public DotationService Dotations() => new(Env.Fabrique(Organisme), AccesExecution(), new AuditFactice());
        public AlerteService Alertes() => new(Env.Fabrique(Organisme), Env.Droits, Notifications, NullLogger<AlerteService>.Instance);
        public EngagementService Engagements() => new(Env.Fabrique(Organisme), AccesExecution(), new AuditFactice(), Alertes(), Env.Droits, Notifications);
        public PaiementService Paiements() => new(Env.Fabrique(Organisme), AccesExecution(), new AuditFactice());
        public ExerciceService Exercices() => new(Env.Fabrique(Organisme), new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new AuditFactice());
    }

    private static readonly TypeDroit[] DroitsGestionnaire =
    [
        TypeDroit.ExecutionLire, TypeDroit.DotationSaisir, TypeDroit.EngagementSaisir, TypeDroit.EngagementValider,
        TypeDroit.DepassementValider, TypeDroit.PaiementSaisir, TypeDroit.ParametrageGerer, TypeDroit.ParametrageLire,
        TypeDroit.EntretienSaisir, TypeDroit.InvestissementSaisir, TypeDroit.FonctionnementSaisir, TypeDroit.AppuisSaisir
    ];

    /// <summary>
    /// Organisme provisionné (axes, types de travaux, listes, seuils), une ligne de dépense
    /// par axe, l'exercice 2027 en préparation ; un gestionnaire détenant tous les droits
    /// des axes et du cycle, habilité en saisie sur tout l'organisme.
    /// </summary>
    private static async Task<Scene> PreparerAsync()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var gestionnaire = await env.CreerUtilisateurAsync("gestion", org);
        env.Droits.Accorder(gestionnaire.Id, DroitsGestionnaire);
        env.UtilisateurCourant.Utilisateur = gestionnaire;

        await using var ctx = env.Contexte(org);
        await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
        await ctx.SaveChangesAsync();

        var source = (await ctx.SourcesFinancement.FirstAsync(s => s.Code == "RI")).Id;
        var lignes = new Dictionary<string, LigneBudgetaire>();
        foreach (var (axeCode, code, nature) in new[] { ("ENTRETIEN", "61", "2"), ("INVESTISSEMENT", "21", "4"), ("FONCTIONNEMENT", "66", "1"), ("APPUIS", "65", "3") })
        {
            var axe = await ctx.Axes.FirstAsync(a => a.Code == axeCode);
            var rubrique = new Rubrique { AxeId = axe.Id, Code = code, Libelle = $"Rubrique {code}" };
            var sr = new SousRubrique { Rubrique = rubrique, Code = code + "1", Libelle = $"Sous-rubrique {code}" };
            var ligne = new LigneBudgetaire
            {
                SousRubrique = sr, Code = code + "100", Libelle = $"Ligne {axeCode}", Sens = SensLigne.Depense,
                NatureDepenseId = (await ctx.NaturesDepense.FirstAsync(n => n.Code == nature)).Id, SourceFinancementId = source
            };
            ctx.AddRange(rubrique, sr, ligne);
            lignes[axeCode] = ligne;
        }
        var exercice = new Exercice { Annee = 2027, DateDebut = new(2027, 1, 1), DateFin = new(2027, 12, 31), Statut = StatutExercice.EnPreparation };
        for (var mois = 1; mois <= 12; mois++)
            exercice.Periodes.Add(new Periode
            {
                TypePeriode = TypePeriode.Mois, Numero = mois,
                DateDebut = new(2027, mois, 1), DateFin = new(2027, mois, DateTime.DaysInMonth(2027, mois))
            });
        ctx.Exercices.Add(exercice);
        ctx.Habilitations.Add(new Habilitation
        {
            UtilisateurId = gestionnaire.Id, TypePerimetre = TypePerimetre.OrganismeEntier, NiveauAcces = NiveauAcces.Saisie,
            DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = gestionnaire.Id
        });
        foreach (var d in DroitsGestionnaire) ctx.UtilisateurDroits.Add(new UtilisateurDroit { UtilisateurId = gestionnaire.Id, TypeDroit = d });
        await ctx.SaveChangesAsync();

        return new Scene(env, org, gestionnaire, exercice.Id, lignes, new NotificationsFactices());
    }

    private static async Task<Ulid> TiersAsync(Scene s, string code, TypeTiers type, Ulid? parentId = null)
    {
        await using var ctx = s.Env.Contexte(s.Organisme);
        var t = new Tiers { Code = code, RaisonSociale = $"Tiers {code}", TypeTiers = type, TiersParentId = parentId };
        ctx.Tiers.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private static async Task<Ulid> IdAsync<T>(Scene s, Func<IQueryable<T>, IQueryable<Ulid>> requete) where T : class
    {
        await using var ctx = s.Env.Contexte(s.Organisme);
        return await requete(ctx.Set<T>()).FirstAsync();
    }

    private static async Task<Ulid> ProjetAsync(Scene s, SaisieProjet saisie)
    {
        var (ok, erreur, id) = await s.Projets().EnregistrerAsync(saisie);
        Assert.True(ok, erreur);
        return id;
    }

    /// <summary>Dotation saisie sur la ligne de l'axe, l'exercice étant en préparation.</summary>
    private static async Task<Ulid> DotationAsync(Scene s, string axe, decimal montant, Ulid? projetId = null, Ulid? tiersId = null, Ulid? siteId = null)
    {
        var r = await s.Dotations().EnregistrerAsync(new SaisieDotation
        {
            ExerciceId = s.ExerciceId, LigneBudgetaireId = s.Lignes[axe].Id, ProjetId = projetId, TiersId = tiersId, SiteId = siteId, MontantInitial = montant
        });
        Assert.True(r.Ok, r.Erreur);
        await using var ctx = s.Env.Contexte(s.Organisme);
        return await ctx.Dotations.Where(d => d.LigneBudgetaireId == s.Lignes[axe].Id && d.ProjetId == projetId && d.TiersId == tiersId && d.SiteId == siteId)
                                  .Select(d => d.Id).SingleAsync();
    }

    private static async Task OuvrirAsync(Scene s) => Assert.True((await s.Exercices().OuvrirAsync(s.ExerciceId)).Ok);

    /// <summary>Dotation saisie en préparation, puis exercice ouvert.</summary>
    private static async Task<Ulid> DotationOuverteAsync(Scene s, string axe, decimal montant, Ulid? projetId = null)
    {
        var id = await DotationAsync(s, axe, montant, projetId);
        await OuvrirAsync(s);
        return id;
    }

    private static async Task<Ulid> EngagementValideAsync(Scene s, Ulid dotationId, decimal montant, Ulid? contratId = null)
    {
        var service = s.Engagements();
        var (ok, erreur, id) = await service.EnregistrerAsync(new SaisieEngagement
        {
            DotationId = dotationId, Date = new(2027, 3, 1), Objet = "Engagement de test", Montant = montant, ContratId = contratId
        });
        Assert.True(ok, erreur);
        Assert.True((await service.SoumettreAsync(id, null)).Ok);
        Assert.True((await service.ValiderAsync(id)).Ok);
        return id;
    }

    [Fact]
    public async Task Sections_des_axes_livres()
    {
        // C.2.4 : seuls les investissements relèvent de la section Investissement ;
        // l'entretien routier et les appuis sont imputés en fonctionnement.
        var s = await PreparerAsync();
        await using var ctx = s.Env.Contexte(s.Organisme);
        var sections = await ctx.Axes.ToDictionaryAsync(a => a.Code, a => a.Section);
        Assert.Equal(Section.Fonctionnement, sections["ENTRETIEN"]);
        Assert.Equal(Section.Investissement, sections["INVESTISSEMENT"]);
        Assert.Equal(Section.Fonctionnement, sections["FONCTIONNEMENT"]);
        Assert.Equal(Section.Fonctionnement, sections["APPUIS"]);
    }

    // ── Projets ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Projet_selon_l_axe_droits_exigences_et_complement_travaux()
    {
        var s = await PreparerAsync();

        // La lecture seule de l'axe ne permet pas de créer.
        var lecteur = await s.Env.CreerUtilisateurAsync("lecteur", s.Organisme);
        s.Env.Droits.Accorder(lecteur.Id, TypeDroit.EntretienLire, TypeDroit.InvestissementSaisir);
        s.Env.UtilisateurCourant.Utilisateur = lecteur;
        var refus = await s.Projets().EnregistrerAsync(new SaisieProjet { CodeAxe = "ENTRETIEN", Code = "OP-1", Intitule = "Route A" });
        Assert.False(refus.Ok);
        Assert.Contains("Entretien routier", refus.Erreur);
        s.Env.UtilisateurCourant.Utilisateur = s.Gestionnaire;

        // Fonctionnement : pas de projets (gestion par activité).
        Assert.False((await s.Projets().EnregistrerAsync(new SaisieProjet { CodeAxe = "FONCTIONNEMENT", Code = "F-1", Intitule = "x" })).Ok);

        // Investissement : source de financement obligatoire.
        var sansSource = await s.Projets().EnregistrerAsync(new SaisieProjet { CodeAxe = "INVESTISSEMENT", Code = "INV-1", Intitule = "Poste de pesage" });
        Assert.Contains("source de financement", sansSource.Erreur);

        // Appuis : un bénéficiaire du bon type.
        var prestataire = await TiersAsync(s, "ENT01", TypeTiers.Prestataire);
        var mauvais = await s.Projets().EnregistrerAsync(new SaisieProjet { CodeAxe = "APPUIS", Code = "AP-1", Intitule = "Appui", TiersBeneficiaireId = prestataire });
        Assert.False(mauvais.Ok);

        // Entretien : le complément « travaux » partage l'identifiant du projet.
        var typeTravaux = await IdAsync<TypeTravaux>(s, q => q.Where(t => t.Code == CodesTypeTravaux.Courants).Select(t => t.Id));
        var controle = await IdAsync<TypeTravaux>(s, q => q.Where(t => t.EstPrestationControle).Select(t => t.Id));
        Assert.False((await s.Projets().EnregistrerAsync(new SaisieProjet
        {
            CodeAxe = "ENTRETIEN", Code = "OP-1", Intitule = "Route A", TypeTravauxId = controle
        })).Ok);
        var projetId = await ProjetAsync(s, new SaisieProjet
        {
            CodeAxe = "ENTRETIEN", Code = "OP-1", Intitule = "Route A", TypeTravauxId = typeTravaux, KmPrevus = 40, Localisation = "PK 0 – PK 40"
        });
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var travaux = await ctx.ProjetsTravaux.SingleAsync();
            Assert.Equal(projetId, travaux.Id);
            Assert.Equal(40, travaux.KmPrevus);
        }

        // Code unique dans l'organisme, tous axes confondus.
        var source = await IdAsync<SourceFinancement>(s, q => q.Where(x => x.Code == "RI").Select(x => x.Id));
        var doublon = await s.Projets().EnregistrerAsync(new SaisieProjet { CodeAxe = "INVESTISSEMENT", Code = "OP-1", Intitule = "x", SourceFinancementId = source });
        Assert.Contains("déjà utilisé", doublon.Erreur);

        var fiche = await s.Projets().ObtenirAsync(projetId);
        Assert.Equal(AxesDepense.Entretien, fiche!.Axe);
        Assert.Equal("Travaux courants d'entretien", fiche.TypeTravaux);
    }

    [Fact]
    public async Task Dotation_et_engagement_restent_coherents_avec_le_projet_et_son_contrat()
    {
        var s = await PreparerAsync();
        var source = await IdAsync<SourceFinancement>(s, q => q.Where(x => x.Code == "RI").Select(x => x.Id));
        var investissement = await ProjetAsync(s, new SaisieProjet { CodeAxe = "INVESTISSEMENT", Code = "INV-1", Intitule = "Pesage", SourceFinancementId = source });
        var operation = await ProjetAsync(s, new SaisieProjet { CodeAxe = "ENTRETIEN", Code = "OP-1", Intitule = "Route A" });
        var autre = await ProjetAsync(s, new SaisieProjet { CodeAxe = "ENTRETIEN", Code = "OP-2", Intitule = "Route B" });

        // Un projet d'investissement ne se dote pas sur une ligne d'entretien.
        var croisee = await s.Dotations().EnregistrerAsync(new SaisieDotation
        {
            ExerciceId = s.ExerciceId, LigneBudgetaireId = s.Lignes["ENTRETIEN"].Id, ProjetId = investissement, MontantInitial = 1_000
        });
        Assert.Contains("autre axe", croisee.Erreur);

        var dotation = await DotationOuverteAsync(s, "ENTRETIEN", 1_000_000, operation);
        var entreprise = await TiersAsync(s, "BTP", TypeTiers.Prestataire);
        Assert.True((await s.Contrats().EnregistrerAsync(new SaisieContrat
        {
            ProjetId = autre, Numero = "M-002", Objet = "Travaux route B", Type = TypeContrat.Travaux, TitulaireId = entreprise, MontantInitial = 500_000
        })).Ok);
        var contratAutre = await IdAsync<Contrat>(s, q => q.Where(c => c.Numero == "M-002").Select(c => c.Id));

        // L'engagement d'une dotation de projet ne porte que sur un contrat de ce projet.
        var (ok, erreur, _) = await s.Engagements().EnregistrerAsync(new SaisieEngagement
        {
            DotationId = dotation, Date = new(2027, 3, 1), Objet = "x", Montant = 1_000, ContratId = contratAutre
        });
        Assert.False(ok);
        Assert.Contains("projet de la dotation", erreur);
    }

    // ── Contrats ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Contrats_de_travaux_et_prestations_de_controle()
    {
        var s = await PreparerAsync();
        var operation = await ProjetAsync(s, new SaisieProjet { CodeAxe = "ENTRETIEN", Code = "OP-1", Intitule = "Route A", KmPrevus = 40 });
        var autre = await ProjetAsync(s, new SaisieProjet { CodeAxe = "ENTRETIEN", Code = "OP-2", Intitule = "Route B" });
        var entreprise = await TiersAsync(s, "BTP", TypeTiers.Prestataire);
        var bureau = await TiersAsync(s, "BET", TypeTiers.Prestataire);
        var entite = await TiersAsync(s, "MIN", TypeTiers.EntiteBeneficiaireAppuis);
        var controle = await IdAsync<ListeValeur>(s, q => q.Where(l => l.Categorie == CategoriesListeValeur.TypePrestation && l.Code == "CONTROLE").Select(l => l.Id));
        var service = s.Contrats();

        Assert.True((await service.EnregistrerAsync(new SaisieContrat
        {
            ProjetId = operation, Numero = "M-001", Objet = "Travaux route A", Type = TypeContrat.Travaux, TitulaireId = entreprise,
            MontantInitial = 800_000, MontantAvenants = 50_000, KmContractuels = 40, EstPluriannuel = true
        })).Ok);
        var travaux = await IdAsync<Contrat>(s, q => q.Where(c => c.Numero == "M-001").Select(c => c.Id));
        Assert.True((await service.EnregistrerAsync(new SaisieContrat
        {
            ProjetId = autre, Numero = "M-002", Objet = "Travaux route B", Type = TypeContrat.Travaux, TitulaireId = entreprise, MontantInitial = 100
        })).Ok);
        var travauxAutre = await IdAsync<Contrat>(s, q => q.Where(c => c.Numero == "M-002").Select(c => c.Id));

        // Prestation : type obligatoire ; elle contrôle un contrat de travaux de la même opération.
        var prestation = new SaisieContrat
        {
            ProjetId = operation, Numero = "P-001", Objet = "Contrôle route A", Type = TypeContrat.Prestation, TitulaireId = bureau, MontantInitial = 40_000
        };
        Assert.Contains("type de prestation", (await service.EnregistrerAsync(prestation)).Erreur);
        prestation.TypePrestationId = controle;
        prestation.ContratTravauxId = travauxAutre;
        Assert.Contains("même opération", (await service.EnregistrerAsync(prestation)).Erreur);
        prestation.ContratTravauxId = travaux;
        Assert.True((await service.EnregistrerAsync(prestation)).Ok);

        // Une convention d'appui n'a pas sa place sur l'entretien ; le titulaire suit le type de contrat.
        Assert.False((await service.EnregistrerAsync(new SaisieContrat
        {
            ProjetId = operation, Numero = "C-001", Objet = "x", Type = TypeContrat.ConventionAppui, TitulaireId = entite, MontantInitial = 1
        })).Ok);
        Assert.Contains("prestataire", (await service.EnregistrerAsync(new SaisieContrat
        {
            ProjetId = operation, Numero = "M-003", Objet = "x", Type = TypeContrat.Travaux, TitulaireId = entite, MontantInitial = 1
        })).Erreur);
        Assert.Contains("déjà utilisé", (await service.EnregistrerAsync(new SaisieContrat
        {
            ProjetId = operation, Numero = "M-001", Objet = "x", Type = TypeContrat.Travaux, TitulaireId = entreprise, MontantInitial = 1
        })).Erreur);

        // Le contrat de travaux contrôlé ne se supprime pas ; engagé et payé remontent sur le contrat.
        Assert.Contains("prestations", (await service.SupprimerAsync(travaux)).Erreur);
        var dotation = await DotationOuverteAsync(s, "ENTRETIEN", 1_000_000, operation);
        var engagement = await EngagementValideAsync(s, dotation, 600_000, travaux);
        Assert.True((await s.Paiements().PayerAsync(new SaisiePaiement
        {
            EngagementId = engagement, NumeroPiece = "PC-1", Date = new(2027, 4, 1), Montant = 200_000
        })).Ok);

        var (contrats, total) = await service.ListerAsync(AxesDepense.Entretien, new FiltreContrats { ProjetId = operation });
        Assert.Equal(2, total);
        var ligne = contrats.Single(c => c.Id == travaux);
        Assert.Equal(850_000, ligne.Montant);
        Assert.Equal(600_000, ligne.Engage);
        Assert.Equal(200_000, ligne.Paye);
        Assert.Equal(70.59m, ligne.TauxEngagement);
        Assert.False(ligne.EngageAuDela);
        Assert.Equal("M-001", contrats.Single(c => c.Type == TypeContrat.Prestation).ContratTravaux);
    }

    // ── Avancement physique ─────────────────────────────────────────────────

    [Fact]
    public async Task Avancement_physique_par_mois_et_type_de_travaux()
    {
        var s = await PreparerAsync();
        var revetu = await IdAsync<TypeTravaux>(s, q => q.Where(t => t.Code == CodesTypeTravaux.Courants).Select(t => t.Id));
        var pistes = await IdAsync<TypeTravaux>(s, q => q.Where(t => t.Code == CodesTypeTravaux.Periodiques).Select(t => t.Id));
        var operation = await ProjetAsync(s, new SaisieProjet { CodeAxe = "ENTRETIEN", Code = "OP-1", Intitule = "Route A", TypeTravauxId = revetu, KmPrevus = 40 });
        await ProjetAsync(s, new SaisieProjet { CodeAxe = "ENTRETIEN", Code = "OP-2", Intitule = "Pistes", TypeTravauxId = pistes, KmPrevus = 60 });
        var mars = await IdAsync<Periode>(s, q => q.Where(p => p.TypePeriode == TypePeriode.Mois && p.Numero == 3).Select(p => p.Id));
        var avril = await IdAsync<Periode>(s, q => q.Where(p => p.TypePeriode == TypePeriode.Mois && p.Numero == 4).Select(p => p.Id));
        var service = s.Avancements();

        // Exercice en préparation : pas de saisie.
        var saisie = new SaisieAvancement { ProjetId = operation, PeriodeId = mars, KmRealises = 12 };
        Assert.Contains("exercice ouvert", (await service.EnregistrerAsync(saisie)).Erreur);
        await OuvrirAsync(s);

        // Sans type précisé, celui de l'opération s'applique ; un relevé par mois, contrat et type.
        Assert.True((await service.EnregistrerAsync(saisie)).Ok);
        Assert.Contains("déjà saisi", (await service.EnregistrerAsync(new SaisieAvancement { ProjetId = operation, PeriodeId = mars, KmRealises = 3 })).Erreur);
        Assert.True((await service.EnregistrerAsync(new SaisieAvancement { ProjetId = operation, PeriodeId = avril, KmRealises = 13, TauxAvancement = 62.5m })).Ok);
        Assert.Contains("0 et 100", (await service.EnregistrerAsync(new SaisieAvancement { ProjetId = operation, PeriodeId = avril, TauxAvancement = 120 })).Erreur);

        // Réhabilitation (C.2.4) : un investissement de type réhabilitation, refusé en entretien, suivi en km.
        var rehabilitation = await IdAsync<TypeTravaux>(s, q => q.Where(t => t.Code == CodesTypeTravaux.Rehabilitation && !t.EstPrestationControle).Select(t => t.Id));
        var source = await IdAsync<SourceFinancement>(s, q => q.Where(x => x.Code == "RI").Select(x => x.Id));
        Assert.Contains("Investissements", (await s.Projets().EnregistrerAsync(new SaisieProjet
            { CodeAxe = "ENTRETIEN", Code = "RH-0", Intitule = "x", TypeTravauxId = rehabilitation })).Erreur);
        Assert.False((await s.Projets().EnregistrerAsync(new SaisieProjet
            { CodeAxe = "INVESTISSEMENT", Code = "RH-0", Intitule = "x", SourceFinancementId = source, TypeTravauxId = revetu })).Ok);
        var rehab = await ProjetAsync(s, new SaisieProjet
            { CodeAxe = "INVESTISSEMENT", Code = "RH-1", Intitule = "Réhabilitation RN1", SourceFinancementId = source, TypeTravauxId = rehabilitation, KmPrevus = 20 });
        var pesage = await ProjetAsync(s, new SaisieProjet { CodeAxe = "INVESTISSEMENT", Code = "INV-1", Intitule = "Pesage", SourceFinancementId = source });
        Assert.True((await service.EnregistrerAsync(new SaisieAvancement { ProjetId = rehab, PeriodeId = mars, KmRealises = 5 })).Ok);
        Assert.Contains("opération de travaux", (await service.EnregistrerAsync(new SaisieAvancement { ProjetId = pesage, PeriodeId = mars, KmRealises = 1 })).Erreur);
        var operations = await s.Projets().ListerOperationsTravauxAsync();
        Assert.Contains(operations, o => o.Id == rehab && o.Libelle.EndsWith("(réhabilitation)"));
        Assert.DoesNotContain(operations, o => o.Id == pesage);
        var (investissements, _) = await s.Projets().ListerAsync(AxesDepense.Investissement, new FiltreProjets());
        Assert.Equal(25m, investissements.Single(p => p.Id == rehab).TauxKm);

        var synthese = await service.SyntheseAsync(s.ExerciceId);
        Assert.Equal(5, synthese.Single(k => k.TypeTravauxId == rehabilitation).KmCumules);
        var ligneRevetu = synthese.Single(k => k.TypeTravauxId == revetu);
        Assert.Equal(40, ligneRevetu.KmPrevus);
        Assert.Equal(25, ligneRevetu.KmCumules);
        Assert.Equal(62.5m, ligneRevetu.Taux);
        Assert.Equal(0, synthese.Single(k => k.TypeTravauxId == pistes).KmCumules);

        var (projets, _) = await s.Projets().ListerAsync(AxesDepense.Entretien, new FiltreProjets { TypeTravauxId = revetu });
        Assert.Equal(62.5m, Assert.Single(projets).TauxKm);

        // Un mois clos refuse la saisie.
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            (await ctx.Periodes.FirstAsync(p => p.Id == avril)).EstCloturee = true;
            await ctx.SaveChangesAsync();
        }
        Assert.Contains("clos", (await service.EnregistrerAsync(new SaisieAvancement { ProjetId = operation, PeriodeId = avril, TypeTravauxId = pistes, KmRealises = 1 })).Erreur);
    }

    // ── Fonctionnement ──────────────────────────────────────────────────────

    [Fact]
    public async Task Fonctionnement_par_activite_selon_les_cles_et_personnel_face_aux_effectifs()
    {
        var s = await PreparerAsync();
        var ligne = s.Lignes["FONCTIONNEMENT"];
        Ulid activiteA, activiteB;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var programme = new Programme { Code = "P1", Libelle = "Programme 1" };
            var action = new ActionProgramme { Programme = programme, Code = "A1", Libelle = "Action 1" };
            var a = new Activite { Action = action, Code = "ACT1", Libelle = "Gestion" };
            var b = new Activite { Action = action, Code = "ACT2", Libelle = "Pilotage" };
            ctx.AddRange(programme, action, a, b);
            ctx.ClesRepartition.AddRange(
                new CleRepartition { ExerciceId = s.ExerciceId, LigneBudgetaireId = ligne.Id, Activite = a, Pourcentage = 60 },
                new CleRepartition { ExerciceId = s.ExerciceId, LigneBudgetaireId = ligne.Id, Activite = b, Pourcentage = 40 });
            var categorie = new CategorieEmploi { Code = "CS", Libelle = "Cadres supérieurs" };
            var structure = new Structure { Code = "DG", Libelle = "Direction générale" };
            ctx.AddRange(categorie, structure,
                new Agent { Matricule = "A1", NomComplet = "Agent 1", CategorieEmploi = categorie, Structure = structure },
                new Agent { Matricule = "A2", NomComplet = "Agent 2", CategorieEmploi = categorie, Structure = structure });
            await ctx.SaveChangesAsync();
            (activiteA, activiteB) = (a.Id, b.Id);
        }

        var dotation = await DotationOuverteAsync(s, "FONCTIONNEMENT", 1_000_000);
        await EngagementValideAsync(s, dotation, 500_000);

        var parActivite = await s.Synthese().RegrouperAsync(AxesDepense.Fonctionnement, s.ExerciceId, Regroupement.Activite);
        Assert.Equal(600_000, parActivite.Single(l => l.Id == activiteA).Montants.DotationTotale);
        Assert.Equal(200_000, parActivite.Single(l => l.Id == activiteB).Montants.AETotal);
        Assert.Equal(1_000_000, MontantsDotation.Somme(parActivite.Select(l => l.Montants)).DotationTotale);

        var parNature = await s.Synthese().RegrouperAsync(AxesDepense.Fonctionnement, s.ExerciceId, Regroupement.Nature);
        Assert.StartsWith("1 — Personnel", Assert.Single(parNature).Libelle);

        // Ligne de nature « Personnel » : mise en regard des 2 agents actifs (pas de cycle rattaché).
        var personnel = await s.Synthese().PersonnelAsync(s.ExerciceId);
        Assert.Equal(1_000_000, personnel!.Montants.DotationTotale);
        Assert.Equal(2, personnel.TotalAgents);
        Assert.Null(personnel.DotationParEmploi);
        Assert.Null(personnel.Cycle);

        // Seuils globaux 80 / 90 / 100 % : à 50 % la ligne n'est pas encore sous tension.
        Assert.Empty(await s.Synthese().ProchesDepassementAsync(AxesDepense.Fonctionnement, s.ExerciceId));
        await EngagementValideAsync(s, dotation, 350_000);
        Assert.Equal(80m, Assert.Single(await s.Synthese().ProchesDepassementAsync(AxesDepense.Fonctionnement, s.ExerciceId)).Seuil);
    }

    // ── Appuis ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Appuis_par_entite_versements_et_attente_de_versement()
    {
        var s = await PreparerAsync();
        var tutelle = await TiersAsync(s, "MIN", TypeTiers.EntiteBeneficiaireAppuis);
        var cellule = await TiersAsync(s, "MIN-CEL", TypeTiers.EntiteBeneficiaireAppuis, tutelle);
        var commune = await TiersAsync(s, "COM", TypeTiers.EntiteBeneficiaireAppuis);
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var site = new Site { Code = "PEAGE1", Nom = "Péage nord", TypeSite = TypeSite.PostePeage };
            ctx.Sites.Add(site);
            ctx.SitesTiers.Add(new SiteTiers { Site = site, TiersId = commune });
            await ctx.SaveChangesAsync();
        }

        // Un appui à la cellule (sous-entité), porté par le projet ; un appui direct à la commune.
        var projet = await ProjetAsync(s, new SaisieProjet { CodeAxe = "APPUIS", Code = "AP-1", Intitule = "Appui cellule", TiersBeneficiaireId = cellule });
        var dotationCellule = await DotationAsync(s, "APPUIS", 300_000, projetId: projet);
        var dotationCommune = await DotationAsync(s, "APPUIS", 200_000, tiersId: commune);
        await OuvrirAsync(s);
        var appuiCellule = await EngagementValideAsync(s, dotationCellule, 300_000);
        var appuiCommune = await EngagementValideAsync(s, dotationCommune, 200_000);
        Assert.True((await s.Paiements().PayerAsync(new SaisiePaiement
        {
            EngagementId = appuiCellule, NumeroPiece = "V-1", Date = new(2027, 5, 1), Montant = 100_000
        })).Ok);
        Assert.True((await s.Paiements().PayerAsync(new SaisiePaiement
        {
            EngagementId = appuiCommune, NumeroPiece = "V-2", Date = new(2027, 5, 2), Montant = 200_000
        })).Ok);

        var synthese = s.Synthese();
        var entites = await synthese.AppuisParEntiteAsync(s.ExerciceId);
        var ligneCellule = entites.Single(e => e.Id == cellule);
        Assert.Equal("Tiers MIN", ligneCellule.Parent);
        Assert.Equal(200_000, ligneCellule.Solde);                   // prévu − versé
        Assert.Equal(1, ligneCellule.NbEnAttente);
        Assert.Equal("Péage nord", entites.Single(e => e.Id == commune).Sites);
        Assert.Equal(0, entites.Single(e => e.Id == commune).NbEnAttente);

        // Historique : la tutelle couvre sa sous-entité.
        var (versements, total, montant) = await synthese.VersementsAsync(new FiltreVersements { ExerciceId = s.ExerciceId, TiersId = tutelle });
        Assert.Equal(1, total);
        Assert.Equal(100_000, montant);
        Assert.Equal("V-1", Assert.Single(versements).NumeroPiece);

        var enAttente = Assert.Single(await synthese.AppuisEnAttenteAsync(s.ExerciceId));
        Assert.Equal(200_000, enAttente.Reste);
        Assert.Equal("Tiers MIN-CEL", enAttente.Entite);

        // Alerte : pas avant 30 jours, puis une seule fois par mois.
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            Assert.Equal(0, await AlerteAppuis.VerifierAsync(ctx, s.Env.Droits, s.Notifications, DateTime.UtcNow));
            var dans40Jours = DateTime.UtcNow.AddDays(40);
            Assert.Equal(1, await AlerteAppuis.VerifierAsync(ctx, s.Env.Droits, s.Notifications, dans40Jours));
            var (type, cle, destinataires) = Assert.Single(s.Notifications.Groupes, g => g.Type == TypeNotification.AppuiEnAttente);
            Assert.Equal(AlerteAppuis.Cle(appuiCellule, dans40Jours), cle);
            Assert.Contains(s.Gestionnaire.Id, destinataires);

            // Notification enregistrée (le service réel le fait) : pas de second rappel dans le mois.
            ctx.Notifications.Add(new Notification { UtilisateurId = s.Gestionnaire.Id, Type = type, Message = "x", CleEntite = cle });
            await ctx.SaveChangesAsync();
            Assert.Equal(0, await AlerteAppuis.VerifierAsync(ctx, s.Env.Droits, s.Notifications, dans40Jours));
        }
    }

    // ── Investissements ─────────────────────────────────────────────────────

    [Fact]
    public async Task Inventaire_par_site_et_comparaison_inter_sites()
    {
        var s = await PreparerAsync();
        Ulid nord, sud, compteImmo, compteCharge;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var a = new Site { Code = "N", Nom = "Péage nord", TypeSite = TypeSite.PostePeage };
            var b = new Site { Code = "S", Nom = "Pesage sud", TypeSite = TypeSite.PostePesage };
            var immo = new CompteComptable { Numero = "2183", Libelle = "Matériel informatique", Classe = 2, EstImmobilisation = true };
            var charge = new CompteComptable { Numero = "6064", Libelle = "Fournitures", Classe = 6 };
            ctx.AddRange(a, b, immo, charge);
            await ctx.SaveChangesAsync();
            (nord, sud, compteImmo, compteCharge) = (a.Id, b.Id, immo.Id, charge.Id);
        }
        var service = s.Immobilisations();

        var bien = new SaisieImmobilisation
        {
            Code = "INV-001", Designation = "Serveur", SiteId = nord, CompteComptableId = compteCharge, DateAcquisition = new(2026, 6, 1), ValeurAcquisition = 5_000_000
        };
        Assert.Contains("compte d'immobilisation", (await service.EnregistrerAsync(bien)).Erreur);
        bien.CompteComptableId = compteImmo;
        Assert.True((await service.EnregistrerAsync(bien)).Ok);
        Assert.True((await service.EnregistrerAsync(new SaisieImmobilisation
        {
            Code = "INV-002", Designation = "Pont-bascule", SiteId = sud, CompteComptableId = compteImmo, DateAcquisition = new(2026, 7, 1),
            ValeurAcquisition = 12_000_000, Etat = EtatImmobilisation.HorsService
        })).Ok);
        Assert.Contains("déjà attribué", (await service.EnregistrerAsync(new SaisieImmobilisation
        {
            Code = "INV-001", Designation = "x", SiteId = sud, CompteComptableId = compteImmo, DateAcquisition = new(2026, 7, 1)
        })).Erreur);

        var parSite = await service.ParSiteAsync();
        Assert.Equal(5_000_000, parSite.Single(x => x.SiteId == nord).Valeur);
        Assert.Equal(1, parSite.Single(x => x.SiteId == sud).NbHorsService);

        // Dotations par site, puis taux de réalisation comparés.
        var dNord = await DotationAsync(s, "INVESTISSEMENT", 1_000_000, siteId: nord);
        await DotationAsync(s, "INVESTISSEMENT", 2_000_000, siteId: sud);
        await OuvrirAsync(s);
        var engagement = await EngagementValideAsync(s, dNord, 400_000);
        Assert.True((await s.Paiements().PayerAsync(new SaisiePaiement
        {
            EngagementId = engagement, NumeroPiece = "PI-1", Date = new(2027, 6, 1), Montant = 400_000
        })).Ok);

        var sites = await s.Synthese().RegrouperAsync(AxesDepense.Investissement, s.ExerciceId, Regroupement.Site);
        Assert.Equal(40m, sites.Single(x => x.Id == nord).Montants.TauxConsommationYCReports);
        Assert.Equal(0m, sites.Single(x => x.Id == sud).Montants.TauxConsommationYCReports);
        Assert.Equal(3_000_000, (await s.Synthese().TotauxAsync(AxesDepense.Investissement, s.ExerciceId)).DotationTotale);

        // Sans droit sur l'axe, rien n'est visible.
        var autre = await s.Env.CreerUtilisateurAsync("appuis", s.Organisme);
        s.Env.Droits.Accorder(autre.Id, TypeDroit.AppuisLire);
        s.Env.UtilisateurCourant.Utilisateur = autre;
        Assert.Empty(await s.Synthese().RegrouperAsync(AxesDepense.Investissement, s.ExerciceId, Regroupement.Site));
        Assert.Empty(await s.Immobilisations().ParSiteAsync());
    }

    // ── Indicateurs de l'entretien (C.2.1) ─────────────────────────────────

    [Fact]
    public async Task Besoins_et_indicateurs_de_l_entretien()
    {
        var s = await PreparerAsync();
        var besoins = new BesoinEntretienService(s.Env.Fabrique(s.Organisme), s.Acces(), new AuditFactice());
        var dotation = await DotationOuverteAsync(s, "ENTRETIEN", 1_000_000);
        var engagement = await EngagementValideAsync(s, dotation, 600_000);
        Assert.True((await s.Paiements().PayerAsync(new SaisiePaiement { EngagementId = engagement, NumeroPiece = "OP-E1", Date = new(2027, 4, 1), Montant = 300_000 })).Ok);

        // Sans besoins saisis, la couverture n'est pas significative.
        var sansBesoins = (await besoins.IndicateursAsync(s.ExerciceId))!;
        Assert.Null(sansBesoins.TauxCouverture);

        Assert.True((await besoins.EnregistrerAsync(new SaisieBesoin { ExerciceId = s.ExerciceId, Montant = 2_000_000, Commentaire = "Programme d'entretien" })).Ok);
        Assert.Contains("déjà saisi", (await besoins.EnregistrerAsync(new SaisieBesoin { ExerciceId = s.ExerciceId, Montant = 1 })).Erreur);
        var courants = await IdAsync<TypeTravaux>(s, q => q.Where(t => t.Code == CodesTypeTravaux.Courants).Select(t => t.Id));
        Assert.Contains("pas les deux", (await besoins.EnregistrerAsync(new SaisieBesoin { ExerciceId = s.ExerciceId, TypeTravauxId = courants, Montant = 1 })).Erreur);

        var i = (await besoins.IndicateursAsync(s.ExerciceId))!;
        Assert.Equal(1_000_000, i.PrevisionsAffectees);
        Assert.Equal(60m, i.TauxConformite);          // AD / prévisions affectées
        Assert.Equal(50m, i.TauxCouverture);          // financement / besoins évalués
        Assert.Equal(50m, i.TauxExecutionPhysique);   // CP / AD
        Assert.Single(await besoins.ListerAsync(s.ExerciceId));
    }
}
