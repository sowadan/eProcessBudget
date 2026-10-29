using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Consommation;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Infrastructure.Services.Recettes;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace eProcessBudget.Tests;

/// <summary>Tests clés du module Recettes (cahier technique § 4.6).</summary>
public class RecettesTests
{
    // ═════ Formules ═══════════════════════════════════════════════════════════

    [Fact]
    public void Formules_des_recettes()
    {
        var m = new MontantsRecette(PrevisionInitiale: 1_000, PrevisionRetenue: 1_200, Encaisse: 900, EncaissePositionne: 700);
        Assert.Equal(200, m.Revision);
        Assert.Equal(300, m.Ecart);                                   // reste à recouvrer
        Assert.Equal(200, m.EncaisseNonPositionne);
        Assert.Equal(75m, m.TauxRecouvrement);                        // 900 / 1 200
        Assert.Null(MontantsRecette.Zero.TauxRecouvrement);           // « n.s. »
        Assert.Equal(1_200, CalculsRecettes.PrevisionRetenue(1_000, 1_200));
        Assert.Equal(1_000, CalculsRecettes.PrevisionRetenue(1_000, null));

        Assert.Equal(25m, CalculsRecettes.TauxAccroissement(1_250, 1_000));   // (N − N-1) / N-1
        Assert.Equal(-20m, CalculsRecettes.TauxAccroissement(800, 1_000));
        Assert.Null(CalculsRecettes.TauxAccroissement(800, 0));
        Assert.Equal(-150, CalculsRecettes.Bilan(850, 1_000));

        // Les encaissements couvrent les échéances dans l'ordre : 500 couvrent la 1re (300) et 200 de la 2e.
        var couverture = CalculsRecettes.Couverture([(new DateOnly(2027, 6, 30), 300m), (new DateOnly(2027, 3, 31), 300m), (new DateOnly(2027, 9, 30), 300m)],
            500, new DateOnly(2027, 7, 15));
        Assert.Equal([300m, 200m, 0m], couverture.Select(c => c.Couvert));
        Assert.Equal([false, true, false], couverture.Select(c => c.EnRetard));   // la 2e est passée et non couverte
        Assert.Equal("En retard", LibellesRecettes.StatutEcheance(300, 200, true));
    }

    // ═════ Services sur base en mémoire ═══════════════════════════════════════

    private sealed record Scene(Environnement Env, Ulid Organisme, Utilisateur Gestionnaire, Ulid ExerciceId,
                                LigneBudgetaire LignePropre, LigneBudgetaire LigneTaxe, Ulid Peage, Ulid Pesage, NotificationsFactices Notifications)
    {
        public AccesRecettes Acces() => new(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme)));
        public PrevisionRecetteService Previsions() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public EncaissementService Encaissements() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public ReportRessourceService Reports() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public SyntheseRecetteService Synthese() => new(Env.Fabrique(Organisme), Acces());
        public EmissionRecetteService Emissions() => new(Env.Fabrique(Organisme), Acces(), new AuditFactice());
        public CompteSurChiffresService Chiffres() => new(Env.Fabrique(Organisme),
            new AccesConsommation(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme))), Acces());
        public ExerciceService Exercices() => new(Env.Fabrique(Organisme), new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new AuditFactice());
        public AccesExecution AccesExecution() => new(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme)));

        public async Task<Ulid> NatureAsync(string code)
        {
            await using var ctx = Env.Contexte(Organisme);
            return await ctx.NaturesRecette.Where(n => n.Code == code).Select(n => n.Id).FirstAsync();
        }
    }

    private static readonly TypeDroit[] DroitsGestionnaire =
    [
        TypeDroit.RecettesLire, TypeDroit.PrevisionRecetteSaisir, TypeDroit.EncaissementSaisir,
        TypeDroit.ParametrageGerer, TypeDroit.ParametrageLire, TypeDroit.ExecutionLire, TypeDroit.DotationSaisir,
        TypeDroit.EngagementSaisir, TypeDroit.EngagementValider, TypeDroit.PaiementSaisir, TypeDroit.ExerciceCloturer
    ];

    /// <summary>
    /// Organisme provisionné (natures de recettes, axe Recettes), deux lignes de recette
    /// (recettes propres, taxes affectées), deux postes (péage, pesage), l'exercice 2027 en
    /// préparation ; un gestionnaire des recettes habilité sur tout l'organisme.
    /// </summary>
    private static async Task<Scene> PreparerAsync()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var gestionnaire = await env.CreerUtilisateurAsync("recettes", org);
        env.Droits.Accorder(gestionnaire.Id, DroitsGestionnaire);
        env.UtilisateurCourant.Utilisateur = gestionnaire;

        await using var ctx = env.Contexte(org);
        await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
        await ctx.SaveChangesAsync();

        var axe = await ctx.Axes.FirstAsync(a => a.Code == "RECETTES");
        var source = (await ctx.SourcesFinancement.FirstAsync(s => s.Code == "RI")).Id;
        LigneBudgetaire Ligne(string code, string nature) => new()
        {
            SousRubrique = new SousRubrique { Rubrique = new Rubrique { AxeId = axe.Id, Code = code, Libelle = $"Rubrique {code}" }, Code = code + "1", Libelle = code },
            Code = code + "100", Libelle = $"Ligne {code}", Sens = SensLigne.Recette, SourceFinancementId = source,
            NatureRecetteId = ctx.NaturesRecette.Local.Concat(ctx.NaturesRecette).First(n => n.Code == nature).Id
        };
        var propre = Ligne("70", "RP");
        var taxe = Ligne("74", "TA");
        var peage = new Site { Code = "P1", Nom = "Péage nord", TypeSite = TypeSite.PostePeage };
        var pesage = new Site { Code = "P2", Nom = "Pesage sud", TypeSite = TypeSite.PostePesage };
        var exercice = NouvelExercice(2027);
        ctx.AddRange(propre, taxe, peage, pesage, exercice);
        ctx.Habilitations.Add(new Habilitation
        {
            UtilisateurId = gestionnaire.Id, TypePerimetre = TypePerimetre.OrganismeEntier, NiveauAcces = NiveauAcces.Saisie,
            DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = gestionnaire.Id
        });
        foreach (var d in DroitsGestionnaire) ctx.UtilisateurDroits.Add(new UtilisateurDroit { UtilisateurId = gestionnaire.Id, TypeDroit = d });
        await ctx.SaveChangesAsync();

        return new Scene(env, org, gestionnaire, exercice.Id, propre, taxe, peage.Id, pesage.Id, new NotificationsFactices());
    }

    private static Exercice NouvelExercice(int annee, StatutExercice statut = StatutExercice.EnPreparation)
    {
        var e = new Exercice { Annee = annee, DateDebut = new(annee, 1, 1), DateFin = new(annee, 12, 31), Statut = statut };
        for (var mois = 1; mois <= 12; mois++)
            e.Periodes.Add(new Periode
            {
                TypePeriode = TypePeriode.Mois, Numero = mois,
                DateDebut = new(annee, mois, 1), DateFin = new(annee, mois, DateTime.DaysInMonth(annee, mois))
            });
        return e;
    }

    private static async Task<Ulid> PrevisionAsync(Scene s, LigneBudgetaire ligne, decimal montant, string? nature = null, Ulid? site = null)
    {
        var natureId = nature is null ? (Ulid?)null : await s.NatureAsync(nature);
        var (ok, erreur) = await s.Previsions().EnregistrerAsync(new SaisiePrevision
        {
            ExerciceId = s.ExerciceId, LigneBudgetaireId = ligne.Id, NatureRecetteId = natureId, SiteId = site, MontantInitial = montant
        });
        Assert.True(ok, erreur);
        await using var ctx = s.Env.Contexte(s.Organisme);
        return await ctx.PrevisionsRecettes.Where(p => p.LigneBudgetaireId == ligne.Id && p.SiteId == site && (natureId == null || p.NatureRecetteId == natureId))
                                           .Select(p => p.Id).FirstAsync();
    }

    private static async Task OuvrirAsync(Scene s) => Assert.True((await s.Exercices().OuvrirAsync(s.ExerciceId)).Ok);

    private static async Task EncaisserAsync(Scene s, Ulid previsionId, DateOnly date, decimal montant, Ulid? compte = null)
    {
        var (ok, erreur) = await s.Encaissements().EnregistrerAsync(new SaisieEncaissement
        {
            PrevisionRecetteId = previsionId, Date = date, Montant = montant, CompteBancaireId = compte
        });
        Assert.True(ok, erreur);
    }

    // ── Prévisions ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Prevision_nature_site_et_unicite()
    {
        var s = await PreparerAsync();
        var service = s.Previsions();

        // Sans le droit de prévision : refus, même avec celui d'encaisser.
        var caissier = await s.Env.CreerUtilisateurAsync("caisse", s.Organisme);
        s.Env.Droits.Accorder(caissier.Id, TypeDroit.EncaissementSaisir);
        s.Env.UtilisateurCourant.Utilisateur = caissier;
        Assert.False((await service.EnregistrerAsync(new SaisiePrevision { ExerciceId = s.ExerciceId, LigneBudgetaireId = s.LignePropre.Id, MontantInitial = 1 })).Ok);
        s.Env.UtilisateurCourant.Utilisateur = s.Gestionnaire;

        // Le péage se suit par poste : le site est exigé.
        var peage = await s.NatureAsync("RP_PEAGE");
        var sansSite = await service.EnregistrerAsync(new SaisiePrevision { ExerciceId = s.ExerciceId, LigneBudgetaireId = s.LignePropre.Id, NatureRecetteId = peage, MontantInitial = 1_000 });
        Assert.Contains("site", sansSite.Erreur);

        // Une nature d'une autre famille que celle de la ligne est refusée.
        var accise = await s.NatureAsync("TA_ACCISE");
        Assert.Contains("sous-natures", (await service.EnregistrerAsync(new SaisiePrevision
        {
            ExerciceId = s.ExerciceId, LigneBudgetaireId = s.LignePropre.Id, NatureRecetteId = accise, MontantInitial = 1
        })).Erreur);

        await PrevisionAsync(s, s.LignePropre, 1_000_000, "RP_PEAGE", s.Peage);
        await PrevisionAsync(s, s.LignePropre, 400_000, "RP_PEAGE", s.Pesage);
        Assert.Contains("existe déjà", (await service.EnregistrerAsync(new SaisiePrevision
        {
            ExerciceId = s.ExerciceId, LigneBudgetaireId = s.LignePropre.Id, NatureRecetteId = peage, SiteId = s.Peage, MontantInitial = 5
        })).Erreur);

        // Sans nature précisée, celle de la ligne ; les reports ne se prévoient pas ici.
        await PrevisionAsync(s, s.LigneTaxe, 2_000_000);
        var (lignes, total) = await service.ListerAsync(new FiltrePrevisions { ExerciceId = s.ExerciceId });
        Assert.Equal(3, total);
        Assert.Equal(CategorieRecette.TaxeAffectee, lignes.Single(l => l.LigneBudgetaireId == s.LigneTaxe.Id).Categorie);
        Assert.Equal(3_400_000, (await service.TotauxAsync(new FiltrePrevisions { ExerciceId = s.ExerciceId })).PrevisionInitiale);

        // Une ligne de dépense n'accueille pas de prévision de recette.
        await using var ctx = s.Env.Contexte(s.Organisme);
        var depense = await ctx.Axes.FirstAsync(a => a.Code == "FONCTIONNEMENT");
        var ligneDepense = new LigneBudgetaire
        {
            SousRubrique = new SousRubrique { Rubrique = new Rubrique { AxeId = depense.Id, Code = "60", Libelle = "Achats" }, Code = "601", Libelle = "x" },
            Code = "60100", Libelle = "Achats", Sens = SensLigne.Depense, SourceFinancementId = s.LignePropre.SourceFinancementId,
            NatureDepenseId = (await ctx.NaturesDepense.FirstAsync()).Id
        };
        ctx.LignesBudgetaires.Add(ligneDepense);
        await ctx.SaveChangesAsync();
        Assert.Contains("ligne de recette", (await service.EnregistrerAsync(new SaisiePrevision { ExerciceId = s.ExerciceId, LigneBudgetaireId = ligneDepense.Id })).Erreur);
    }

    [Fact]
    public async Task Prevision_figee_a_l_ouverture_puis_revision_motivee_et_echeancier()
    {
        var s = await PreparerAsync();
        var service = s.Previsions();
        var prevision = await PrevisionAsync(s, s.LigneTaxe, 1_200_000);

        // En préparation, pas de révision : on modifie l'initiale.
        Assert.Contains("exercice ouvert", (await service.ReviserAsync(prevision, 1_500_000, "Hausse")).Erreur);
        await OuvrirAsync(s);

        // Ouvert : l'initiale est figée ; une nouvelle prévision naît à 0.
        Assert.Contains("révisez", (await service.EnregistrerAsync(new SaisiePrevision
        {
            Id = prevision, ExerciceId = s.ExerciceId, LigneBudgetaireId = s.LigneTaxe.Id, MontantInitial = 2_000_000
        })).Erreur);
        Assert.Contains("figée", (await service.EnregistrerAsync(new SaisiePrevision
        {
            ExerciceId = s.ExerciceId, LigneBudgetaireId = s.LigneTaxe.Id, NatureRecetteId = await s.NatureAsync("TA_TVM"), MontantInitial = 10
        })).Erreur);

        // Échéancier : dans l'exercice, au plus la prévision retenue.
        Assert.Contains("dépasse", (await service.DefinirEcheancierAsync(prevision, [(new(2027, 3, 31), 1_000_000m), (new(2027, 6, 30), 300_000m)])).Erreur);
        Assert.Contains("dans l'exercice", (await service.DefinirEcheancierAsync(prevision, [(new(2028, 1, 31), 100m)])).Erreur);
        Assert.True((await service.DefinirEcheancierAsync(prevision, [(new(2027, 3, 31), 600_000m), (new(2027, 6, 30), 600_000m)])).Ok);

        // Révision : motif obligatoire ; pas sous l'échéancier.
        Assert.Contains("motif", (await service.ReviserAsync(prevision, 1_500_000, " ")).Erreur);
        Assert.Contains("échéancier", (await service.ReviserAsync(prevision, 1_000_000, "Baisse")).Erreur);
        Assert.True((await service.ReviserAsync(prevision, 1_500_000, "Hausse de l'assiette")).Ok);

        var ligne = Assert.Single((await service.ListerAsync(new FiltrePrevisions { ExerciceId = s.ExerciceId })).Items);
        Assert.Equal(1_200_000, ligne.Montants.PrevisionInitiale);
        Assert.Equal(1_500_000, ligne.Montants.PrevisionRetenue);
        Assert.Equal("Hausse de l'assiette", ligne.MotifRevision);
        Assert.Equal(2, ligne.NbEcheances);

        // Figée et révisée : elle ne se supprime plus.
        Assert.Contains("figée", (await service.SupprimerAsync(prevision)).Erreur);
    }

    // ── Encaissements ───────────────────────────────────────────────────────

    [Fact]
    public async Task Encaissements_mensuels_positionnement_grille_et_mois_clos()
    {
        var s = await PreparerAsync();
        var peage = await PrevisionAsync(s, s.LignePropre, 1_200_000, "RP_PEAGE", s.Peage);
        var service = s.Encaissements();

        // Exercice en préparation : pas d'encaissement.
        var saisie = new SaisieEncaissement { PrevisionRecetteId = peage, Date = new(2027, 1, 31), Montant = 100_000 };
        Assert.Contains("exercice ouvert", (await service.EnregistrerAsync(saisie)).Erreur);
        await OuvrirAsync(s);

        Ulid compte;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var devise = await ctx.Devises.FirstAsync();
            var c = new CompteBancaire { Banque = "BQ", Intitule = "Compte recettes", NumeroCompte = "001", DeviseId = devise.Id };
            ctx.ComptesBancaires.Add(c);
            await ctx.SaveChangesAsync();
            compte = c.Id;
        }

        await EncaisserAsync(s, peage, new(2027, 1, 31), 100_000, compte);
        await EncaisserAsync(s, peage, new(2027, 1, 15), 50_000);            // non positionné
        await EncaisserAsync(s, peage, new(2027, 2, 28), 120_000, compte);
        Assert.Contains("comprise dans l'exercice", (await service.EnregistrerAsync(new SaisieEncaissement { PrevisionRecetteId = peage, Date = new(2028, 1, 2), Montant = 1 })).Erreur);
        Assert.False((await service.EnregistrerAsync(new SaisieEncaissement { PrevisionRecetteId = peage, Date = new(2027, 3, 1), Montant = 0 })).Ok);

        var (grille, totaux) = await service.GrilleAsync(new FiltrePrevisions { ExerciceId = s.ExerciceId });
        var ligne = Assert.Single(grille);
        Assert.Equal(150_000, ligne.Mois[0]);
        Assert.Equal(120_000, ligne.Mois[1]);
        Assert.Equal(270_000, ligne.Total);
        Assert.Equal(22.5m, ligne.TauxRecouvrement);
        Assert.Equal(150_000, totaux[0]);

        var prevision = Assert.Single((await s.Previsions().ListerAsync(new FiltrePrevisions { ExerciceId = s.ExerciceId })).Items);
        Assert.Equal(220_000, prevision.Montants.EncaissePositionne);
        Assert.Equal(50_000, prevision.Montants.EncaisseNonPositionne);
        var (nonPositionnes, nb, montant) = await service.ListerAsync(new FiltreEncaissements { ExerciceId = s.ExerciceId, EstPositionne = false });
        Assert.Equal(1, nb);
        Assert.Equal(50_000, montant);

        // Mois de janvier clos : plus de saisie, de correction ni de suppression sur ce mois.
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            (await ctx.Periodes.FirstAsync(p => p.TypePeriode == TypePeriode.Mois && p.Numero == 1)).EstCloturee = true;
            await ctx.SaveChangesAsync();
        }
        Assert.Contains("clos", (await service.EnregistrerAsync(new SaisieEncaissement { PrevisionRecetteId = peage, Date = new(2027, 1, 20), Montant = 1 })).Erreur);
        var janvier = nonPositionnes.Single();
        Assert.Contains("clos", (await service.SupprimerAsync(janvier.Id)).Erreur);
        var correction = (await service.ObtenirSaisieAsync(janvier.Id))!;
        correction.Montant = 60_000;
        Assert.Contains("clos", (await service.EnregistrerAsync(correction)).Erreur);

        // Une prévision encaissée ne se supprime pas.
        Assert.Contains("encaissements", (await s.Previsions().SupprimerAsync(peage)).Erreur);
    }

    // ── Synthèse, reports, échéances, bilan ─────────────────────────────────

    [Fact]
    public async Task Indicateurs_reports_et_accroissement_a_periode_comparable()
    {
        var s = await PreparerAsync();
        var peage = await PrevisionAsync(s, s.LignePropre, 1_000_000, "RP_PEAGE", s.Peage);
        var pesage = await PrevisionAsync(s, s.LignePropre, 500_000, "RP_PESAGE", s.Pesage);
        await OuvrirAsync(s);

        // N-1 : 2026, encaissements de janvier à mars (100 000 chacun) et d'avril (500 000), au péage.
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var n1 = NouvelExercice(2026, StatutExercice.Cloture);
            var prevN1 = new PrevisionRecette
            {
                Exercice = n1, LigneBudgetaireId = s.LignePropre.Id, NatureRecetteId = await s.NatureAsync("RP_PEAGE"), SiteId = s.Peage, MontantInitial = 900_000
            };
            ctx.AddRange(n1, prevN1);
            foreach (var (mois, montant) in new[] { (1, 100_000m), (2, 100_000m), (3, 100_000m), (4, 500_000m) })
                ctx.Encaissements.Add(new Encaissement
                {
                    Exercice = n1, Periode = n1.Periodes.Single(p => p.Numero == mois), PrevisionRecette = prevN1,
                    DateEncaissement = new(2026, mois, 10), Montant = montant, Statut = StatutSaisie.Valide
                });
            await ctx.SaveChangesAsync();
        }

        // N : janvier à mars.
        await EncaisserAsync(s, peage, new(2027, 1, 10), 120_000);
        await EncaisserAsync(s, peage, new(2027, 2, 10), 120_000);
        await EncaisserAsync(s, pesage, new(2027, 3, 10), 120_000);

        // Reports : la nature doit être un report ; positionnés ou non.
        Assert.Contains("Report de ressources", (await s.Reports().EnregistrerAsync(new SaisieReportRessource
        {
            ExerciceId = s.ExerciceId, NatureRecetteId = await s.NatureAsync("RP_PEAGE"), Montant = 1
        })).Erreur);
        Assert.True((await s.Reports().EnregistrerAsync(new SaisieReportRessource
        {
            ExerciceId = s.ExerciceId, NatureRecetteId = await s.NatureAsync("RR_TRESORERIE"), Montant = 300_000, EstPositionne = true
        })).Ok);
        Assert.True((await s.Reports().EnregistrerAsync(new SaisieReportRessource
        {
            ExerciceId = s.ExerciceId, NatureRecetteId = await s.NatureAsync("RR_AVOIRS"), Montant = 200_000
        })).Ok);

        var i = (await s.Synthese().IndicateursAsync(s.ExerciceId))!;
        Assert.Equal(360_000, i.Niveau);
        Assert.Equal(24m, i.Montants.TauxRecouvrement);                 // 360 000 / 1 500 000
        Assert.Equal(3, i.MoisCompares);                                // jusqu'à mars
        Assert.Equal(300_000, i.EncaisseN1Comparable);                  // avril 2026 exclu
        Assert.Equal(20m, i.TauxAccroissement);                         // (360 − 300) / 300
        Assert.Equal(500_000, i.Reports);
        Assert.Equal(200_000 + 360_000, i.NonPositionne);               // aucun compte saisi en N

        var parSite = await s.Synthese().RegrouperAsync(s.ExerciceId, RegroupementRecette.Site);
        Assert.Equal(240_000, parSite.Single(l => l.Id == s.Peage).Montants.Encaisse);
        var parCategorie = Assert.Single(await s.Synthese().RegrouperAsync(s.ExerciceId, RegroupementRecette.Categorie));
        Assert.Equal("Recette propre", parCategorie.Libelle);

        var sites = await s.Synthese().ComparaisonSitesAsync(s.ExerciceId);
        var nord = sites.Single(x => x.SiteId == s.Peage);
        Assert.Equal(800_000, nord.AnneeN1);
        Assert.Null(nord.AnneeN2);                                      // aucune donnée en 2025
        Assert.Equal(240_000, nord.AnneeN);

        var mensuel = await s.Synthese().MensuelAsync(s.ExerciceId);
        Assert.Equal(120_000, mensuel[0].Encaisse);
        Assert.Equal(500_000, mensuel[3].EncaisseN1);
    }

    [Fact]
    public async Task Echeances_en_retard_et_alerte_unique()
    {
        var s = await PreparerAsync();
        var prevision = await PrevisionAsync(s, s.LigneTaxe, 1_000_000);
        await OuvrirAsync(s);
        Assert.True((await s.Previsions().DefinirEcheancierAsync(prevision, [(new(2027, 3, 31), 400_000m), (new(2027, 6, 30), 400_000m)])).Ok);
        await EncaisserAsync(s, prevision, new(2027, 3, 15), 500_000);

        var echeancier = await s.Previsions().ObtenirEcheancierAsync(prevision);
        Assert.Equal(400_000, echeancier[0].Couvert);
        Assert.Equal(100_000, echeancier[1].Couvert);

        await using var ctx = s.Env.Contexte(s.Organisme);
        // Avant le 30 juin, rien n'est en retard ; après, la 2e échéance l'est, et n'est signalée qu'une fois.
        Assert.Equal(0, await AlerteRecouvrement.VerifierAsync(ctx, s.Env.Droits, s.Notifications, new DateOnly(2027, 6, 30)));
        Assert.Equal(1, await AlerteRecouvrement.VerifierAsync(ctx, s.Env.Droits, s.Notifications, new DateOnly(2027, 7, 1)));
        var (type, cle, destinataires) = Assert.Single(s.Notifications.Groupes);
        Assert.Equal(TypeNotification.EcheanceRecouvrement, type);
        Assert.Equal(AlerteRecouvrement.Cle(echeancier[1].Id), cle);
        Assert.Contains(s.Gestionnaire.Id, destinataires);
        ctx.Notifications.Add(new Notification { UtilisateurId = s.Gestionnaire.Id, Type = type, Message = "x", CleEntite = cle });
        await ctx.SaveChangesAsync();
        Assert.Equal(0, await AlerteRecouvrement.VerifierAsync(ctx, s.Env.Droits, s.Notifications, new DateOnly(2027, 7, 2)));
    }

    [Fact]
    public async Task Bilan_recettes_depenses_et_cloture_vers_l_historique()
    {
        var s = await PreparerAsync();
        var prevision = await PrevisionAsync(s, s.LignePropre, 1_000_000, "RP_PEAGE", s.Peage);

        // Une dépense de 800 000 au Fonctionnement, dont 600 000 payés.
        Ulid ligneDepense;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var axe = await ctx.Axes.FirstAsync(a => a.Code == "FONCTIONNEMENT");
            var ligne = new LigneBudgetaire
            {
                SousRubrique = new SousRubrique { Rubrique = new Rubrique { AxeId = axe.Id, Code = "60", Libelle = "Achats" }, Code = "601", Libelle = "x" },
                Code = "60100", Libelle = "Achats", Sens = SensLigne.Depense, SourceFinancementId = s.LignePropre.SourceFinancementId,
                NatureDepenseId = (await ctx.NaturesDepense.FirstAsync()).Id
            };
            ctx.LignesBudgetaires.Add(ligne);
            ctx.Exercices.Add(NouvelExercice(2028));
            await ctx.SaveChangesAsync();
            ligneDepense = ligne.Id;
        }
        var dotations = new DotationService(s.Env.Fabrique(s.Organisme), s.AccesExecution(), new AuditFactice());
        Assert.True((await dotations.EnregistrerAsync(new SaisieDotation { ExerciceId = s.ExerciceId, LigneBudgetaireId = ligneDepense, MontantInitial = 800_000 })).Ok);
        await OuvrirAsync(s);
        Ulid dotationId;
        await using (var ctx = s.Env.Contexte(s.Organisme))
            dotationId = await ctx.Dotations.Select(d => d.Id).SingleAsync();
        var alertes = new AlerteService(s.Env.Fabrique(s.Organisme), s.Env.Droits, s.Notifications, NullLogger<AlerteService>.Instance);
        var engagements = new EngagementService(s.Env.Fabrique(s.Organisme), s.AccesExecution(), new AuditFactice(), alertes, s.Env.Droits, s.Notifications);
        var (_, _, engagementId) = await engagements.EnregistrerAsync(new SaisieEngagement { DotationId = dotationId, Date = new(2027, 2, 1), Objet = "Achats", Montant = 600_000 });
        Assert.True((await engagements.SoumettreAsync(engagementId, null)).Ok);
        Assert.True((await engagements.ValiderAsync(engagementId)).Ok);
        Assert.True((await new PaiementService(s.Env.Fabrique(s.Organisme), s.AccesExecution(), new AuditFactice()).PayerAsync(new SaisiePaiement
        {
            EngagementId = engagementId, NumeroPiece = "PC-1", Date = new(2027, 3, 1), Montant = 600_000
        })).Ok);

        await EncaisserAsync(s, prevision, new(2027, 4, 30), 700_000);
        Assert.True((await s.Reports().EnregistrerAsync(new SaisieReportRessource
        {
            ExerciceId = s.ExerciceId, NatureRecetteId = await s.NatureAsync("RR_TRESORERIE"), Montant = 100_000, EstPositionne = true
        })).Ok);

        var bilan = (await s.Synthese().BilanAsync(s.ExerciceId))!;
        Assert.Equal(1_100_000, bilan.RessourcesPrevues);              // 1 000 000 + report 100 000
        Assert.Equal(800_000, bilan.RessourcesRealisees);              // 700 000 encaissés + 100 000 reportés
        Assert.Equal(800_000, bilan.DepensesPrevues);
        Assert.Equal(600_000, bilan.DepensesRealisees);
        Assert.Equal(200_000, bilan.Bilan);                            // 800 000 − 600 000
        Assert.Equal(300_000, bilan.EquilibrePrevisionnel);
        Assert.Equal("Fonctionnement", Assert.Single(bilan.Depenses).Libelle);
        Assert.Equal(100_000, bilan.Positionne);
        Assert.Equal(700_000, bilan.NonPositionne);

        // La clôture verse aussi les recettes à l'historique.
        var cloture = new ClotureService(s.Env.Fabrique(s.Organisme), s.AccesExecution(), new AuditFactice());
        var (ok, erreur, _) = await cloture.CloturerAsync(s.ExerciceId);
        Assert.True(ok, erreur);
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var recette = await ctx.ExecutionsHistoriques.SingleAsync(h => h.NatureRecetteId != null);
            Assert.Equal(1_000_000, recette.Prevision);
            Assert.Equal(700_000, recette.MontantRealise);
            Assert.Equal(s.Peage, recette.SiteId);
        }
    }

    // ── Compte sur chiffres et équilibre (C.2.7, C.2.9) ────────────────────

    [Fact]
    public void Equilibre_reproduit_le_tableau_2025_du_metier()
    {
        // Totaux généraux 2025 transmis par le métier.
        var recettes = new ChiffresRecette(60_154_304_000, 63_042_282_689, 60_242_587_515);
        var depenses = new ChiffresDepense(60_154_304_000, 60_311_669_378, 0, 30_794_962_412);
        Assert.Equal(29_516_706_966, depenses.ResteAPayer);
        Assert.Equal(103.64m, new ChiffresRecette(15_000_000_000, 15_545_689_145, 14_250_215_050).TauxExecution);
        Assert.Equal(91.67m, new ChiffresRecette(15_000_000_000, 15_545_689_145, 14_250_215_050).TauxRecouvrement);

        var e = new EquilibreTresorerie(recettes.Recouvre, depenses.Paiements + depenses.Rejets, depenses.ResteAPayer,
            TaxesNonRecouvrees: 1_275_484_420 + 215_252_286, RecettesPropresNonRecouvrees: 1_295_474_095 + 13_484_373,
            recettes.Emis, depenses.PriseEnCharge);
        Assert.Equal(29_447_625_103, e.TresorerieSansEncours);      // C
        Assert.Equal(-69_081_863, e.TresorerieDisponible);          // E
        Assert.Equal(2_730_613_311, e.ReportsRessources);           // H
        Assert.Equal(2_730_613_311, e.SoldeEmisPrisEnCharge);       // I
        Assert.Equal(0, e.Ecart);

        Assert.True(CalculsPresentation.EstSignificative(1_150, 1_000));
        Assert.False(CalculsPresentation.EstSignificative(1_050, 1_000));
        Assert.True(CalculsPresentation.EstSignificative(10, 0));
    }

    [Fact]
    public async Task Factures_emises_compte_sur_chiffres_et_equilibre_H_egal_I()
    {
        var s = await PreparerAsync();
        s.Env.Droits.Accorder(s.Gestionnaire.Id, TypeDroit.ConsommationLire);
        var peage = await PrevisionAsync(s, s.LignePropre, 1_000_000, "RP_PEAGE", s.Peage);
        var taxe = await PrevisionAsync(s, s.LigneTaxe, 500_000);

        // Dépense : 800 000 au chapitre 60, AD de 600 000, facture de 600 000 payée à 450 000.
        Ulid ligneDepense;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var axe = await ctx.Axes.FirstAsync(a => a.Code == "FONCTIONNEMENT");
            var ligne = new LigneBudgetaire
            {
                SousRubrique = new SousRubrique { Rubrique = new Rubrique { AxeId = axe.Id, Code = "60", Libelle = "Achats" }, Code = "601", Libelle = "x" },
                Code = "60100", Libelle = "Achats", Sens = SensLigne.Depense, SourceFinancementId = s.LignePropre.SourceFinancementId,
                NatureDepenseId = (await ctx.NaturesDepense.FirstAsync()).Id
            };
            ctx.LignesBudgetaires.Add(ligne);
            await ctx.SaveChangesAsync();
            ligneDepense = ligne.Id;
        }
        var dotations = new DotationService(s.Env.Fabrique(s.Organisme), s.AccesExecution(), new AuditFactice());
        Assert.True((await dotations.EnregistrerAsync(new SaisieDotation { ExerciceId = s.ExerciceId, LigneBudgetaireId = ligneDepense, MontantInitial = 800_000 })).Ok);

        // Une facture émise ne se saisit que sur un exercice ouvert.
        Assert.False((await s.Emissions().EnregistrerAsync(new SaisieEmission { PrevisionRecetteId = peage, Date = new(2027, 2, 1), Montant = 1 })).Ok);
        await OuvrirAsync(s);

        Ulid dotationId;
        await using (var ctx = s.Env.Contexte(s.Organisme))
            dotationId = await ctx.Dotations.Select(d => d.Id).SingleAsync();
        var alertes = new AlerteService(s.Env.Fabrique(s.Organisme), s.Env.Droits, s.Notifications, NullLogger<AlerteService>.Instance);
        var engagements = new EngagementService(s.Env.Fabrique(s.Organisme), s.AccesExecution(), new AuditFactice(), alertes, s.Env.Droits, s.Notifications);
        var (_, _, engagementId) = await engagements.EnregistrerAsync(new SaisieEngagement { DotationId = dotationId, Date = new(2027, 2, 1), Objet = "Achats", Montant = 600_000 });
        Assert.True((await engagements.SoumettreAsync(engagementId, null)).Ok);
        Assert.True((await engagements.ValiderAsync(engagementId)).Ok);
        var paiements = new PaiementService(s.Env.Fabrique(s.Organisme), s.AccesExecution(), new AuditFactice());
        Assert.True((await paiements.AjouterFactureAsync(new SaisieFacture
        {
            EngagementId = engagementId, Numero = "FA-1", DateFacture = new(2027, 2, 10), DateReception = new(2027, 2, 12), Montant = 600_000
        })).Ok);
        Assert.True((await paiements.PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "PC-1", Date = new(2027, 3, 1), Montant = 450_000 })).Ok);

        // Recettes : émis puis recouvré ; report de trésorerie constaté 100 000, inscrit 120 000.
        Assert.True((await s.Emissions().EnregistrerAsync(new SaisieEmission { PrevisionRecetteId = peage, Date = new(2027, 2, 1), Montant = 900_000, Reference = "T-1" })).Ok);
        Assert.True((await s.Emissions().EnregistrerAsync(new SaisieEmission { PrevisionRecetteId = taxe, Date = new(2027, 2, 1), Montant = 500_000 })).Ok);
        await EncaisserAsync(s, peage, new(2027, 3, 15), 700_000);
        await EncaisserAsync(s, taxe, new(2027, 3, 15), 400_000);
        Assert.True((await s.Reports().EnregistrerAsync(new SaisieReportRessource
        {
            ExerciceId = s.ExerciceId, NatureRecetteId = await s.NatureAsync("RR_TRESORERIE"), Montant = 100_000, MontantPrevu = 120_000, EstPositionne = true
        })).Ok);
        Assert.Contains("factures émises", (await s.Previsions().SupprimerAsync(taxe)).Erreur);

        var (journal, nb, montant) = await s.Emissions().ListerAsync(new FiltreEncaissements { ExerciceId = s.ExerciceId });
        Assert.Equal(2, nb);
        Assert.Equal(1_400_000, montant);

        var r = (await s.Chiffres().RecettesAsync(s.ExerciceId))!;
        var lignePeage = r.Lignes.Single(l => l.Code == "70100");
        Assert.Equal(90m, lignePeage.Chiffres.TauxExecution);
        Assert.Equal(200_000, lignePeage.Chiffres.ResteARecouvrer);
        Assert.Equal(new ChiffresRecette(120_000, 100_000, 100_000), Assert.Single(r.Reports).Chiffres);
        Assert.Equal(new ChiffresRecette(1_620_000, 1_500_000, 1_200_000), r.TotalGeneral);

        var d = (await s.Chiffres().DepensesAsync(s.ExerciceId))!;
        var chapitre = Assert.Single(d.Chapitres);
        Assert.Equal("60", chapitre.Code);
        Assert.Equal(new ChiffresDepense(800_000, 600_000, 0, 450_000), chapitre.Chiffres);
        Assert.Equal(75m, chapitre.Chiffres.TauxPaiement);
        Assert.Equal(150_000, d.TotalGeneral.ResteAPayer);

        var e = (await s.Chiffres().EquilibreAsync(s.ExerciceId))!;
        Assert.Equal(1_200_000, e.RecettesRecouvrees);              // A
        Assert.Equal(450_000, e.EngagementsPayes);                  // B
        Assert.Equal(150_000, e.EncoursAnterieurs);                 // D
        Assert.Equal(100_000, e.TaxesNonRecouvrees);                // F
        Assert.Equal(200_000, e.RecettesPropresNonRecouvrees);      // G
        Assert.Equal(900_000, e.ReportsRessources);                 // H
        Assert.Equal(900_000, e.SoldeEmisPrisEnCharge);             // I
        Assert.Equal(0, e.Ecart);

        var presentation = (await s.Chiffres().PresentationAsync(s.ExerciceId))!;
        Assert.Equal(2026, presentation.AnneePrecedente);
        Assert.True(Assert.Single(presentation.Depenses).Significative);   // pas d'antériorité
        Assert.Equal(3, presentation.Recettes.Count);
    }
}
