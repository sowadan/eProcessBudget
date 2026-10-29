using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace eProcessBudget.Tests;

/// <summary>Tests clés du cycle budgétaire commun (cahier technique § 4.4).</summary>
public class ExecutionTests
{
    // ═════ Formules du § 4.1 ══════════════════════════════════════════════════

    [Fact]
    public void Formules_de_la_situation_d_une_dotation()
    {
        // Prévision 1 000 ; ajustements +200 ; reports 300 ; AE 900 dont 250 d'encours reportés ;
        // paiements 600 dont 200 sur les encours ; rejets et pénalités 50.
        var m = new MontantsDotation(1_000, 200, 300, AEPeriode: 650, AEReports: 250,
                                     PaiementsPeriode: 400, PaiementsTotal: 600, RejetsPenalites: 50);

        Assert.Equal(1_200, m.PrevisionAjustee);                // initiale ± ajustements
        Assert.Equal(1_500, m.DotationTotale);                  // + reports
        Assert.Equal(900, m.AETotal);
        Assert.Equal(600, m.Disponible);
        Assert.Equal(900, m.Ecart);                             // dotation totale − réalisé
        Assert.Equal(250, m.ResteAPayerPeriode);                // AD de l'exercice − paiements sur ces AD
        Assert.Equal(300, m.ResteAPayerYCReports);
        Assert.Equal(250, m.EncoursAReporter);                  // − rejets ou pénalités (C.2.6)
        Assert.Equal(650, m.ReliquatCredit);                    // ni payé ni en encours

        // Taux sur les deux bases.
        Assert.Equal(54.17m, m.TauxEngagementPeriode);          // 650 / 1 200
        Assert.Equal(60m, m.TauxEngagementYCReports);           // 900 / 1 500
        Assert.Equal(33.33m, m.TauxConsommationPeriode);        // 400 / 1 200
        Assert.Equal(40m, m.TauxConsommationYCReports);         // 600 / 1 500
        Assert.Equal(66.67m, m.TauxConsommationAD);             // 600 / 900

        // Dénominateur nul : « n.s. ».
        Assert.Null(MontantsDotation.Zero.TauxEngagementPeriode);
        Assert.Null(MontantsDotation.Zero.TauxConsommationAD);

        // Somme de plusieurs dotations.
        Assert.Equal(3_000, MontantsDotation.Somme([m, m]).DotationTotale);
    }

    [Fact]
    public void Depassement_seuils_et_numerotation()
    {
        var m = new MontantsDotation(1_000, 0, 0, 800, 0, 0, 0, 0);
        Assert.Equal(0, CalculsExecution.Depassement(m, 200));
        Assert.Equal(50, CalculsExecution.Depassement(m, 250));
        Assert.Equal(105m, CalculsExecution.TauxApres(m, 250));

        Assert.Equal(90m, CalculsExecution.SeuilAtteint(95m, [80, 90, 100]));
        Assert.Null(CalculsExecution.SeuilAtteint(79.99m, [80, 90, 100]));
        Assert.Null(CalculsExecution.SeuilAtteint(null, [80]));

        Assert.Equal("AJ-2026-00001", CalculsExecution.Numero("AJ", 2026, CalculsExecution.RangSuivant(null)));
        Assert.Equal(43, CalculsExecution.RangSuivant("ENG-2026-00042"));
        Assert.Equal("R2027-ENG-2026-00042", ClotureService.NumeroReport("ENG-2026-00042", 2027));
        Assert.Equal("R2028-ENG-2026-00042", ClotureService.NumeroReport("R2027-ENG-2026-00042", 2028));
    }

    // ═════ Services sur base en mémoire ═══════════════════════════════════════

    private sealed record Scene(Environnement Env, Ulid Organisme, Utilisateur Budget, LigneBudgetaire Ligne,
                                Ulid ExerciceId, Ulid SuivantId, NotificationsFactices Notifications)
    {
        public AccesExecution Acces() => new(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme)));
        public AlerteService Alertes() => new(Env.Fabrique(Organisme), Env.Droits, Notifications, NullLogger<AlerteService>.Instance);
        public DotationService Dotations() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public SituationService Situations() => new(Env.Fabrique(Organisme), Acces());
        public AjustementService Ajustements() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice(), Alertes(), Env.Droits, Notifications);
        public EngagementService Engagements() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice(), Alertes(), Env.Droits, Notifications);
        public DepassementService Depassements() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice(), Alertes(), Notifications);
        public PaiementService Paiements() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public ClotureService Cloture() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public ExerciceService Exercices() => new(Env.Fabrique(Organisme), new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new AuditFactice());
    }

    /// <summary>
    /// Organisme provisionné (seuils 80 / 90 / 100 %), une ligne de dépense, l'exercice 2027
    /// en préparation et 2028 créé ; un Responsable Budget détenant tous les droits du
    /// cycle et une habilitation de saisie sur tout l'organisme.
    /// </summary>
    private static async Task<Scene> PreparerAsync()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var budget = await env.CreerUtilisateurAsync("rbudget", org);
        TypeDroit[] droits =
        [
            TypeDroit.ExecutionLire, TypeDroit.DotationSaisir, TypeDroit.AjustementSaisir, TypeDroit.AjustementValider,
            TypeDroit.EngagementSaisir, TypeDroit.EngagementValider, TypeDroit.DepassementValider, TypeDroit.PaiementSaisir,
            TypeDroit.ExerciceCloturer, TypeDroit.ParametrageGerer, TypeDroit.ParametrageLire
        ];
        env.Droits.Accorder(budget.Id, droits);
        env.UtilisateurCourant.Utilisateur = budget;

        await using var ctx = env.Contexte(org);
        await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
        await ctx.SaveChangesAsync();

        var axe = await ctx.Axes.FirstAsync(a => a.Code == "FONCTIONNEMENT");
        var rubrique = new Rubrique { AxeId = axe.Id, Code = "60", Libelle = "Achats" };
        var sr = new SousRubrique { Rubrique = rubrique, Code = "601", Libelle = "Fournitures" };
        var ligne = new LigneBudgetaire
        {
            SousRubrique = sr, Code = "60110", Libelle = "Fournitures", Sens = SensLigne.Depense,
            NatureDepenseId = (await ctx.NaturesDepense.FirstAsync(n => n.Code == "2")).Id,
            SourceFinancementId = (await ctx.SourcesFinancement.FirstAsync(s => s.Code == "RI")).Id
        };
        var exercice = NouvelExercice(2027);
        var suivant = NouvelExercice(2028);
        ctx.AddRange(rubrique, sr, ligne, exercice, suivant, new MotifAjustement { Code = "REVAL", Libelle = "Réévaluation" });
        ctx.Habilitations.Add(new Habilitation
        {
            UtilisateurId = budget.Id, TypePerimetre = TypePerimetre.OrganismeEntier, NiveauAcces = NiveauAcces.Saisie,
            DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = budget.Id
        });
        // Droits aussi en base : c'est là que l'on cherche les destinataires des alertes.
        foreach (var d in droits) ctx.UtilisateurDroits.Add(new UtilisateurDroit { UtilisateurId = budget.Id, TypeDroit = d });
        await ctx.SaveChangesAsync();

        return new Scene(env, org, budget, ligne, exercice.Id, suivant.Id, new NotificationsFactices());
    }

    private static Exercice NouvelExercice(int annee)
    {
        var e = new Exercice { Annee = annee, DateDebut = new(annee, 1, 1), DateFin = new(annee, 12, 31), Statut = StatutExercice.EnPreparation };
        for (var mois = 1; mois <= 12; mois++)
            e.Periodes.Add(new Periode
            {
                TypePeriode = TypePeriode.Mois, Numero = mois,
                DateDebut = new(annee, mois, 1), DateFin = new(annee, mois, DateTime.DaysInMonth(annee, mois))
            });
        return e;
    }

    /// <summary>Dotation de 1 000 000 saisie en préparation, puis exercice ouvert.</summary>
    private static async Task<Ulid> DotationOuverteAsync(Scene s, decimal montant = 1_000_000)
    {
        Assert.True((await s.Dotations().EnregistrerAsync(new SaisieDotation { ExerciceId = s.ExerciceId, LigneBudgetaireId = s.Ligne.Id, MontantInitial = montant })).Ok);
        Assert.True((await s.Exercices().OuvrirAsync(s.ExerciceId)).Ok);
        await using var ctx = s.Env.Contexte(s.Organisme);
        return await ctx.Dotations.Where(d => d.ExerciceId == s.ExerciceId).Select(d => d.Id).SingleAsync();
    }

    /// <summary>Engagement créé, soumis et validé, sans dépassement.</summary>
    private static async Task<Ulid> EngagementValideAsync(Scene s, Ulid dotationId, decimal montant)
    {
        var service = s.Engagements();
        var (ok, erreur, id) = await service.EnregistrerAsync(new SaisieEngagement
        {
            DotationId = dotationId, Date = new(2027, 3, 1), Objet = "Fournitures de bureau", Montant = montant
        });
        Assert.True(ok, erreur);
        Assert.True((await service.SoumettreAsync(id, null)).Ok);
        var validation = await service.ValiderAsync(id);
        Assert.True(validation.Ok && !validation.DepassementSoumis, validation.Erreur);
        return id;
    }

    [Fact]
    public async Task Prevision_initiale_figee_a_l_ouverture_puis_ajustement_motive()
    {
        var s = await PreparerAsync();
        var dotationId = await DotationOuverteAsync(s);

        // Figée : la prévision initiale ne se modifie plus.
        var modification = await s.Dotations().EnregistrerAsync(new SaisieDotation
        {
            Id = dotationId, ExerciceId = s.ExerciceId, LigneBudgetaireId = s.Ligne.Id, MontantInitial = 2_000_000
        });
        Assert.False(modification.Ok);
        Assert.Contains("ajustement", modification.Erreur);

        // Ajustement : motif et justification obligatoires.
        Ulid motifId;
        await using (var ctx = s.Env.Contexte(s.Organisme))
            motifId = await ctx.MotifsAjustement.Select(m => m.Id).FirstAsync();
        var ajustements = s.Ajustements();
        Assert.False((await ajustements.EnregistrerAsync(new SaisieAjustement { DotationId = dotationId, Date = new(2027, 2, 1), Montant = 250_000 })).Ok);
        Assert.True((await ajustements.EnregistrerAsync(new SaisieAjustement
        {
            DotationId = dotationId, Date = new(2027, 2, 1), Montant = 250_000, MotifId = motifId, Justification = "Hausse des prix"
        })).Ok);

        // Soumis : pas encore dans la prévision ajustée.
        var fiche = await s.Situations().ObtenirAsync(dotationId);
        Assert.Equal(1_000_000, fiche!.Dotation.Montants.PrevisionAjustee);
        var ajustement = Assert.Single(fiche.Ajustements);
        Assert.Equal("AJ-2027-00001", ajustement.Numero);

        Assert.True((await ajustements.ValiderAsync(ajustement.Id)).Ok);
        fiche = await s.Situations().ObtenirAsync(dotationId);
        Assert.Equal(1_250_000, fiche!.Dotation.Montants.PrevisionAjustee);

        // Une baisse sous les engagements validés est refusée.
        await EngagementValideAsync(s, dotationId, 1_000_000);
        var baisse = await ajustements.EnregistrerAsync(new SaisieAjustement
        {
            DotationId = dotationId, Date = new(2027, 4, 1), Montant = -300_000, MotifId = motifId, Justification = "Économies"
        });
        Assert.False(baisse.Ok);
        Assert.Contains("engagements validés", baisse.Erreur);
    }

    [Fact]
    public async Task Depassement_non_bloquant_mais_en_attente_de_validation()
    {
        var s = await PreparerAsync();
        var dotationId = await DotationOuverteAsync(s);
        await EngagementValideAsync(s, dotationId, 900_000);

        var engagements = s.Engagements();
        var (_, _, id) = await engagements.EnregistrerAsync(new SaisieEngagement
        {
            DotationId = dotationId, Date = new(2027, 5, 10), Objet = "Commande complémentaire", Montant = 150_000
        });

        var evaluation = await engagements.EvaluerAsync(id);
        Assert.Equal(50_000, evaluation!.Depassement);

        // Le dépassement se motive...
        var sansMotif = await engagements.SoumettreAsync(id, null);
        Assert.False(sansMotif.Ok);
        Assert.Contains("motif", sansMotif.Erreur);
        Assert.True((await engagements.SoumettreAsync(id, "Urgence : rupture de stock")).Ok);
        Assert.Contains(s.Notifications.Groupes, g => g.Type == TypeNotification.DepassementDotation && g.Destinataires.Contains(s.Budget.Id));

        // ... et l'engagement attend la décision : il n'est pas validable directement.
        var validation = await engagements.ValiderAsync(id);
        Assert.False(validation.Ok);
        Assert.Contains("décision", validation.Erreur);
        Assert.Equal(900_000, (await s.Situations().ObtenirAsync(dotationId))!.Dotation.Montants.AETotal);

        // Le Responsable Budget accepte : l'engagement est validé, la dotation dépassée.
        var (demandes, _) = await s.Depassements().ListerAsync(StatutValidation.EnAttente);
        var demande = Assert.Single(demandes);
        Assert.Equal(50_000, demande.MontantDepassement);
        Assert.Equal(105m, demande.TauxApres);
        Assert.True((await s.Depassements().DeciderAsync(demande.Id, accepter: true, "Accordé")).Ok);

        var m = (await s.Situations().ObtenirAsync(dotationId))!.Dotation.Montants;
        Assert.Equal(1_050_000, m.AETotal);
        Assert.Equal(-50_000, m.Disponible);
        Assert.Equal(StatutSaisie.Valide, (await engagements.ObtenirAsync(id))!.Engagement.Statut);

        // Le seuil de 100 % est atteint : une alerte...
        var alertes = s.Notifications.Groupes.Where(g => g.Type == TypeNotification.SeuilConsommation).ToList();
        var cle = Assert.Single(alertes, a => a.Cle!.EndsWith(":Seuil:100")).Cle!;

        // ... une seule fois : une fois la notification enregistrée, le contrôle suivant ne la réémet pas.
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            ctx.Notifications.Add(new Notification { UtilisateurId = s.Budget.Id, Message = "Seuil", CleEntite = cle });
            await ctx.SaveChangesAsync();
        }
        await s.Alertes().VerifierAsync([dotationId]);
        Assert.Single(s.Notifications.Groupes, a => a.Cle == cle);
    }

    [Fact]
    public async Task Paiement_superieur_au_reste_a_payer_ou_hors_exercice_refuse()
    {
        var s = await PreparerAsync();
        var dotationId = await DotationOuverteAsync(s);
        var engagementId = await EngagementValideAsync(s, dotationId, 400_000);
        var paiements = s.Paiements();

        Assert.True((await paiements.PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-001", Date = new(2027, 4, 2), Montant = 300_000 })).Ok);

        // C.2.2 : tout décaissement se rapporte à une facture reçue, dont il ne dépasse pas le reste.
        var sansFacture = await paiements.EnregistrerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-009", Date = new(2027, 4, 3), Montant = 1_000 });
        Assert.Contains("facture", sansFacture.Erreur);
        Assert.True((await paiements.AjouterFactureAsync(new SaisieFacture
        {
            EngagementId = engagementId, Numero = "FAC-2", DateFacture = new(2027, 4, 1), DateReception = new(2027, 4, 2), Montant = 100_000
        })).Ok);
        Assert.False((await paiements.AjouterFactureAsync(new SaisieFacture
        {
            EngagementId = engagementId, Numero = "FAC-3", DateFacture = new(2027, 4, 1), DateReception = new(2027, 4, 2), Montant = 1
        })).Ok);
        var excessif = await paiements.PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-002", Date = new(2027, 4, 3), Montant = 100_000.01m });
        Assert.False(excessif.Ok);
        Assert.Contains("reste à payer", excessif.Erreur);

        var horsExercice = await paiements.PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-003", Date = new(2028, 1, 5), Montant = 1_000 });
        Assert.False(horsExercice.Ok);
        Assert.Contains("exercice", horsExercice.Erreur);

        var doublon = await paiements.PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-001", Date = new(2027, 4, 3), Montant = 1_000 });
        Assert.False(doublon.Ok);

        // Une pénalité réduit d'autant ce qui reste payable.
        Ulid typePenalite;
        await using (var ctx = s.Env.Contexte(s.Organisme))
            typePenalite = await ctx.ListesValeurs.Where(l => l.Categorie == CategoriesListeValeur.TypePenalite).Select(l => l.Id).FirstAsync();
        Assert.True((await s.Engagements().AjouterRejetAsync(new SaisieRejet
        {
            EngagementId = engagementId, TypePenaliteId = typePenalite, Date = new(2027, 4, 10), Montant = 20_000, Motif = "Retard de livraison"
        })).Ok);
        Assert.False((await paiements.PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-004", Date = new(2027, 4, 20), Montant = 90_000 })).Ok);
        Assert.True((await paiements.PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-004", Date = new(2027, 4, 20), Montant = 80_000 })).Ok);

        var m = (await s.Situations().ObtenirAsync(dotationId))!.Dotation.Montants;
        Assert.Equal(380_000, m.PaiementsTotal);
        Assert.Equal(38m, m.TauxConsommationPeriode);
        Assert.Equal(0, m.EncoursAReporter);
    }

    [Fact]
    public async Task Cloture_avec_reliquats_et_encours_reportes()
    {
        var s = await PreparerAsync();
        var dotationId = await DotationOuverteAsync(s);
        var engagementId = await EngagementValideAsync(s, dotationId, 600_000);
        Assert.True((await s.Paiements().PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "OP-1", Date = new(2027, 6, 1), Montant = 450_000 })).Ok);

        // Un engagement en brouillon bloque la clôture.
        var (_, _, brouillon) = await s.Engagements().EnregistrerAsync(new SaisieEngagement { DotationId = dotationId, Date = new(2027, 7, 1), Objet = "À finir", Montant = 1_000 });
        var apercu = await s.Cloture().ApercuAsync(s.ExerciceId);
        Assert.False(apercu!.PeutCloturer);
        Assert.False((await s.Cloture().CloturerAsync(s.ExerciceId)).Ok);
        Assert.True((await s.Engagements().SupprimerAsync(brouillon)).Ok);

        apercu = await s.Cloture().ApercuAsync(s.ExerciceId);
        Assert.True(apercu!.PeutCloturer, string.Join(" ", apercu.Blocages));
        Assert.Equal(150_000, apercu.TotalEncours);
        Assert.Equal(400_000, apercu.TotalReliquats);

        var (ok, erreur, resultat) = await s.Cloture().CloturerAsync(s.ExerciceId);
        Assert.True(ok, erreur);
        Assert.Equal(1, resultat!.NbEncours);
        Assert.Equal(1, resultat.NbDotationsCreees);

        await using var ctx = s.Env.Contexte(s.Organisme);
        Assert.Equal(StatutExercice.Cloture, (await ctx.Exercices.FirstAsync(e => e.Id == s.ExerciceId)).Statut);

        // Sur 2028 : une dotation (prévision 0), deux reports, l'encours devenu engagement reporté.
        var cible = await ctx.Dotations.SingleAsync(d => d.ExerciceId == s.SuivantId);
        Assert.Equal(0, cible.MontantInitial);
        var reports = await ctx.Reports.Where(r => r.DotationCibleId == cible.Id).ToListAsync();
        Assert.Equal(150_000, reports.Single(r => r.TypeReport == TypeReport.EncoursEngagement).Montant);
        Assert.Equal(400_000, reports.Single(r => r.TypeReport == TypeReport.ReliquatCredit).Montant);
        var reporte = await ctx.Engagements.SingleAsync(e => e.ExerciceId == s.SuivantId);
        Assert.True(reporte.EstReport);
        Assert.Equal(engagementId, reporte.EngagementOrigineId);
        Assert.Equal("R2028-ENG-2027-00001", reporte.Numero);

        var historique = await ctx.ExecutionsHistoriques.SingleAsync(h => h.Annee == 2027);
        Assert.Equal(600_000, historique.MontantAutorise);
        Assert.Equal(450_000, historique.MontantRealise);

        // Sur 2028 ouvert, la dotation reçue présente reports et encours sur la base « y compris reports ».
        Assert.True((await s.Exercices().OuvrirAsync(s.SuivantId)).Ok);
        var m = (await s.Situations().ObtenirAsync(cible.Id))!.Dotation.Montants;
        Assert.Equal(550_000, m.DotationTotale);
        Assert.Equal(150_000, m.AEReports);
        Assert.Null(m.TauxEngagementPeriode);                   // prévision 0 : « n.s. »
        Assert.Equal(27.27m, m.TauxEngagementYCReports);       // 150 000 / 550 000
    }

    [Fact]
    public async Task Deplacer_un_engagement_exige_le_perimetre_de_sa_dotation_d_origine()
    {
        var s = await PreparerAsync();
        var a = new Structure { Code = "SA", Libelle = "Service A" };
        var b = new Structure { Code = "SB", Libelle = "Service B" };
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            ctx.Structures.AddRange(a, b);
            await ctx.SaveChangesAsync();
        }
        foreach (var structure in new[] { a, b })
            Assert.True((await s.Dotations().EnregistrerAsync(new SaisieDotation
            {
                ExerciceId = s.ExerciceId, LigneBudgetaireId = s.Ligne.Id, StructureId = structure.Id, MontantInitial = 100_000
            })).Ok);
        Assert.True((await s.Exercices().OuvrirAsync(s.ExerciceId)).Ok);
        Ulid dotationA, dotationB;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            dotationA = await ctx.Dotations.Where(d => d.StructureId == a.Id).Select(d => d.Id).SingleAsync();
            dotationB = await ctx.Dotations.Where(d => d.StructureId == b.Id).Select(d => d.Id).SingleAsync();
        }

        // Brouillon du service B, saisi par le Responsable Budget (habilité sur tout l'organisme).
        var (_, _, engagementId) = await s.Engagements().EnregistrerAsync(new SaisieEngagement
        {
            DotationId = dotationB, Date = new(2027, 3, 1), Objet = "Fournitures du service B", Montant = 10_000
        });

        // Un agent habilité sur le seul service A ne peut pas le rapatrier sur sa dotation.
        var agent = await s.Env.CreerUtilisateurAsync("agentA", s.Organisme);
        s.Env.Droits.Accorder(agent.Id, TypeDroit.ExecutionLire, TypeDroit.EngagementSaisir);
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            ctx.Habilitations.Add(new Habilitation
            {
                UtilisateurId = agent.Id, TypePerimetre = TypePerimetre.Structure, PerimetreId = a.Id, NiveauAcces = NiveauAcces.Saisie,
                DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = agent.Id
            });
            await ctx.SaveChangesAsync();
        }
        s.Env.UtilisateurCourant.Utilisateur = agent;

        var deplacement = await s.Engagements().EnregistrerAsync(new SaisieEngagement
        {
            Id = engagementId, DotationId = dotationA, Date = new(2027, 3, 1), Objet = "Fournitures du service B", Montant = 10_000
        });
        Assert.False(deplacement.Ok);
        Assert.Contains("habilitation", deplacement.Erreur);

        // Sur son propre service, la saisie reste admise.
        Assert.True((await s.Engagements().EnregistrerAsync(new SaisieEngagement
        {
            DotationId = dotationA, Date = new(2027, 3, 2), Objet = "Fournitures du service A", Montant = 5_000
        })).Ok);
    }
}
