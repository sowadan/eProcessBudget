using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.Consommation;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace eProcessBudget.Tests;

/// <summary>Tests clés de la consommation budgétaire et du tableau de bord (cahier technique § 4.7).</summary>
public class ConsommationTests
{
    // ═════ Formules ═══════════════════════════════════════════════════════════

    [Fact]
    public void Projection_lineaire_et_derapage()
    {
        DateOnly debut = new(2027, 1, 1), fin = new(2027, 12, 31);
        Assert.Equal(0, CalculsConsommation.PartEcoulee(debut, fin, new(2026, 12, 31)));
        Assert.Equal(1, CalculsConsommation.PartEcoulee(debut, fin, new(2028, 1, 1)));
        Assert.Equal(90m / 365, CalculsConsommation.PartEcoulee(debut, fin, new(2027, 3, 31)));   // 31 + 28 + 31 jours

        Assert.Equal(1_000_000, CalculsConsommation.Projection(250_000, 0.25m));
        Assert.Null(CalculsConsommation.Projection(250_000, 0));

        var m = new MontantsDotation(1_000, 0, 0, 800, 0, 0, 300, 0);
        Assert.False(CalculsConsommation.Derape(m, 900));
        Assert.True(CalculsConsommation.Derape(m, 1_200));                         // projection au-delà de la dotation
        Assert.True(CalculsConsommation.Derape(m with { AEPeriode = 1_100 }, null)); // engagé au-delà

        Assert.Equal("T2 2027", LibellesConsommation.Periode(TypePeriode.Trimestre, 2, new(2027, 4, 1)));
        Assert.Equal("Travaux", LibellesConsommation.Volets["ENTRETIEN"]);
    }

    // ═════ Services sur base en mémoire ═══════════════════════════════════════

    private sealed record Scene(
        Environnement Env, Ulid Organisme, Utilisateur Gestionnaire, Ulid ExerciceId,
        LigneBudgetaire LigneFonct, LigneBudgetaire LigneEntretien, Ulid AxeEntretien,
        Ulid ProgrammeA, Ulid ProgrammeB, Ulid ActiviteA1, Ulid ActiviteA2, NotificationsFactices Notifications)
    {
        public AccesExecution AccesExecution() => new(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme)));
        public AccesConsommation Acces() => new(new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new ControlePerimetre(Env.Fabrique(Organisme)));
        public ConsommationService Consommation() => new(Env.Fabrique(Organisme), Acces());
        public TableauDeBordService TableauDeBord(IMemoryCache cache) => new(Env.Fabrique(Organisme), Acces(), Env.UtilisateurCourant, cache);
        public DotationService Dotations() => new(Env.Fabrique(Organisme), AccesExecution(), new AuditFactice());
        public EngagementService Engagements() => new(Env.Fabrique(Organisme), AccesExecution(), new AuditFactice(),
            new AlerteService(Env.Fabrique(Organisme), Env.Droits, Notifications, NullLogger<AlerteService>.Instance), Env.Droits, Notifications);
        public PaiementService Paiements() => new(Env.Fabrique(Organisme), AccesExecution(), new AuditFactice());
        public ExerciceService Exercices() => new(Env.Fabrique(Organisme), new AccesParametrage(Env.UtilisateurCourant, Env.Droits), new AuditFactice());
        public FiltreConsommation Filtre() => new() { ExerciceId = ExerciceId };
    }

    private static readonly TypeDroit[] DroitsGestionnaire =
    [
        TypeDroit.ConsommationLire, TypeDroit.TableauDeBordLire, TypeDroit.ExecutionLire, TypeDroit.DotationSaisir, TypeDroit.EngagementSaisir,
        TypeDroit.EngagementValider, TypeDroit.DepassementValider, TypeDroit.PaiementSaisir, TypeDroit.ParametrageGerer, TypeDroit.ParametrageLire
    ];

    /// <summary>
    /// Deux lignes de dépense : Fonctionnement (dotation 1 000 000, clé 60 % activité A1 /
    /// 40 % A2 du programme A) et Entretien (dotation 2 000 000, 100 % au programme B) ;
    /// exercice 2027 ouvert, avec ses mois et ses trimestres ; programmation 2027 des AE / CP.
    /// </summary>
    private static async Task<Scene> PreparerAsync()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var gestionnaire = await env.CreerUtilisateurAsync("pilotage", org);
        env.Droits.Accorder(gestionnaire.Id, DroitsGestionnaire);
        env.UtilisateurCourant.Utilisateur = gestionnaire;

        await using var ctx = env.Contexte(org);
        await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
        await ctx.SaveChangesAsync();

        var source = (await ctx.SourcesFinancement.FirstAsync(s => s.Code == "RI")).Id;
        async Task<LigneBudgetaire> Ligne(string axeCode, string code, string nature)
        {
            var axe = await ctx.Axes.FirstAsync(a => a.Code == axeCode);
            return new LigneBudgetaire
            {
                SousRubrique = new SousRubrique { Rubrique = new Rubrique { AxeId = axe.Id, Code = code, Libelle = $"Rubrique {code}" }, Code = code + "1", Libelle = $"SR {code}" },
                Code = code + "100", Libelle = $"Ligne {code}", Sens = SensLigne.Depense, SourceFinancementId = source,
                NatureDepenseId = (await ctx.NaturesDepense.FirstAsync(n => n.Code == nature)).Id
            };
        }
        var fonct = await Ligne("FONCTIONNEMENT", "60", "2");
        var entretien = await Ligne("ENTRETIEN", "61", "2");
        var axeEntretien = (await ctx.Axes.FirstAsync(a => a.Code == "ENTRETIEN")).Id;

        var cycle = new CycleProgrammation { AnneeBase = 2027, Libelle = "2027-2029", Statut = StatutCycle.Valide };
        var exercice = new Exercice { Annee = 2027, DateDebut = new(2027, 1, 1), DateFin = new(2027, 12, 31), Statut = StatutExercice.EnPreparation, CycleProgrammation = cycle };
        for (var mois = 1; mois <= 12; mois++)
            exercice.Periodes.Add(new Periode { TypePeriode = TypePeriode.Mois, Numero = mois, DateDebut = new(2027, mois, 1), DateFin = new(2027, mois, DateTime.DaysInMonth(2027, mois)) });
        for (var t = 1; t <= 4; t++)
            exercice.Periodes.Add(new Periode { TypePeriode = TypePeriode.Trimestre, Numero = t, DateDebut = new(2027, 3 * t - 2, 1), DateFin = new DateOnly(2027, 3 * t, 1).AddMonths(1).AddDays(-1) });

        var programmeA = new Programme { Code = "PA", Libelle = "Programme A", ResponsableId = gestionnaire.Id };
        var programmeB = new Programme { Code = "PB", Libelle = "Programme B", ResponsableId = gestionnaire.Id };
        var actionA = new ActionProgramme { Programme = programmeA, Code = "A1", Libelle = "Action A" };
        var actionB = new ActionProgramme { Programme = programmeB, Code = "B1", Libelle = "Action B" };
        var a1 = new Activite { Action = actionA, Code = "ACT-A1", Libelle = "Activité A1" };
        var a2 = new Activite { Action = actionA, Code = "ACT-A2", Libelle = "Activité A2" };
        var b1 = new Activite { Action = actionB, Code = "ACT-B1", Libelle = "Activité B1" };
        ctx.AddRange(fonct, entretien, cycle, exercice, programmeA, programmeB, actionA, actionB, a1, a2, b1);
        ctx.ClesRepartition.AddRange(
            new CleRepartition { Exercice = exercice, LigneBudgetaire = fonct, Activite = a1, Pourcentage = 60 },
            new CleRepartition { Exercice = exercice, LigneBudgetaire = fonct, Activite = a2, Pourcentage = 40 },
            new CleRepartition { Exercice = exercice, LigneBudgetaire = entretien, Activite = b1, Pourcentage = 100 });
        var nature = (await ctx.NaturesDepense.FirstAsync(n => n.Code == "2")).Id;
        ctx.ProgrammationsFinancieres.AddRange(
            new ProgrammationFinanciere { CycleProgrammation = cycle, Annee = 2027, Action = actionA, NatureDepenseId = nature, SourceFinancementId = source, MontantAE = 1_000_000, MontantCP = 800_000 },
            new ProgrammationFinanciere { CycleProgrammation = cycle, Annee = 2028, Action = actionA, NatureDepenseId = nature, SourceFinancementId = source, MontantAE = 1_100_000, MontantCP = 900_000 });
        ctx.Habilitations.Add(new Habilitation
        {
            UtilisateurId = gestionnaire.Id, TypePerimetre = TypePerimetre.OrganismeEntier, NiveauAcces = NiveauAcces.Saisie,
            DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = gestionnaire.Id
        });
        await ctx.SaveChangesAsync();

        var s = new Scene(env, org, gestionnaire, exercice.Id, fonct, entretien, axeEntretien, programmeA.Id, programmeB.Id, a1.Id, a2.Id, new NotificationsFactices());
        Assert.True((await s.Dotations().EnregistrerAsync(new SaisieDotation { ExerciceId = exercice.Id, LigneBudgetaireId = fonct.Id, MontantInitial = 1_000_000 })).Ok);
        Assert.True((await s.Dotations().EnregistrerAsync(new SaisieDotation { ExerciceId = exercice.Id, LigneBudgetaireId = entretien.Id, MontantInitial = 2_000_000 })).Ok);
        Assert.True((await s.Exercices().OuvrirAsync(exercice.Id)).Ok);
        return s;
    }

    private static async Task<Ulid> DotationAsync(Scene s, LigneBudgetaire ligne)
    {
        await using var ctx = s.Env.Contexte(s.Organisme);
        return await ctx.Dotations.Where(d => d.LigneBudgetaireId == ligne.Id).Select(d => d.Id).SingleAsync();
    }

    private static async Task<Ulid> EngagerAsync(Scene s, LigneBudgetaire ligne, DateOnly date, decimal montant)
    {
        var (ok, erreur, id) = await s.Engagements().EnregistrerAsync(new SaisieEngagement { DotationId = await DotationAsync(s, ligne), Date = date, Objet = "Test", Montant = montant });
        Assert.True(ok, erreur);
        Assert.True((await s.Engagements().SoumettreAsync(id, "Motif")).Ok);
        Assert.True((await s.Engagements().ValiderAsync(id)).Ok);
        return id;
    }

    private static async Task PayerAsync(Scene s, Ulid engagementId, DateOnly date, decimal montant, string piece)
        => Assert.True((await s.Paiements().PayerAsync(new SaisiePaiement { EngagementId = engagementId, NumeroPiece = piece, Date = date, Montant = montant })).Ok);

    private static async Task<Ulid> PeriodeAsync(Scene s, TypePeriode type, int numero)
    {
        await using var ctx = s.Env.Contexte(s.Organisme);
        return await ctx.Periodes.Where(p => p.ExerciceId == s.ExerciceId && p.TypePeriode == type && p.Numero == numero).Select(p => p.Id).FirstAsync();
    }

    // ── Tableau consolidé, situation à date, programme ──────────────────────

    [Fact]
    public async Task Consolide_tous_axes_a_date_avec_factures_et_part_du_programme()
    {
        var s = await PreparerAsync();
        var engF = await EngagerAsync(s, s.LigneFonct, new(2027, 2, 10), 500_000);
        var engE = await EngagerAsync(s, s.LigneEntretien, new(2027, 3, 5), 800_000);
        await PayerAsync(s, engF, new(2027, 2, 20), 200_000, "P1");
        await PayerAsync(s, engF, new(2027, 5, 20), 100_000, "P2");
        Assert.True((await s.Paiements().AjouterFactureAsync(new SaisieFacture
        {
            EngagementId = engE, Numero = "F-1", DateFacture = new(2027, 3, 20), DateReception = new(2027, 3, 22), Montant = 450_000
        })).Ok);
        await PayerAsync(s, engE, new(2027, 3, 25), 400_000, "P3");   // sur F-1 (C.2.2)

        var service = s.Consommation();
        var courant = (await service.ConsoliderAsync(s.Filtre()))!;
        Assert.Equal(3_000_000, courant.Total.DotationTotale);
        Assert.Equal(1_300_000, courant.Total.AETotal);
        Assert.Equal(700_000, courant.Total.PaiementsTotal);
        Assert.Equal(750_000, courant.Factures);   // F-1 et les factures des paiements P1, P2
        Assert.Equal(2, courant.Axes.Count);

        // Situation à fin mars : le paiement de mai n'est pas encore compté.
        var filtre = s.Filtre();
        filtre.PeriodeId = await PeriodeAsync(s, TypePeriode.Mois, 3);
        var mars = (await service.ConsoliderAsync(filtre))!;
        Assert.Equal(new DateOnly(2027, 3, 31), mars.JusquAu);
        Assert.Equal(600_000, mars.Total.PaiementsTotal);
        Assert.Equal(90m / 365, mars.PartEcoulee);
        Assert.Equal(Math.Round(600_000 / (90m / 365), 2), mars.PaiementsProjetes);   // extrapolation linéaire

        // Programme A : 100 % de la ligne de fonctionnement (60 % A1 + 40 % A2), rien de l'entretien.
        filtre = s.Filtre();
        filtre.ProgrammeId = s.ProgrammeA;
        var programme = (await service.ConsoliderAsync(filtre))!;
        Assert.Equal(1_000_000, programme.Total.DotationTotale);
        Assert.Equal(300_000, programme.Total.PaiementsTotal);

        // Par activité : la clé 60 / 40 ventile les montants.
        var activites = await service.RegrouperAsync(s.Filtre(), Regroupement.Activite);
        Assert.Equal(600_000, activites.Single(a => a.Id == s.ActiviteA1).Montants.DotationTotale);
        Assert.Equal(200_000, activites.Single(a => a.Id == s.ActiviteA2).Montants.AETotal);
        var programmes = await service.RegrouperAsync(s.Filtre(), Regroupement.Programme);
        Assert.Equal(2_000_000, programmes.Single(p => p.Id == s.ProgrammeB).Montants.DotationTotale);
    }

    [Fact]
    public async Task Le_perimetre_de_lecture_restreint_les_donnees()
    {
        var s = await PreparerAsync();
        await EngagerAsync(s, s.LigneFonct, new(2027, 2, 10), 500_000);

        // Habilitation de lecture sur l'axe Entretien : seule sa dotation est visible.
        var lecteur = await s.Env.CreerUtilisateurAsync("lecteur", s.Organisme);
        s.Env.Droits.Accorder(lecteur.Id, TypeDroit.ConsommationLire);
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            ctx.Habilitations.Add(new Habilitation
            {
                UtilisateurId = lecteur.Id, TypePerimetre = TypePerimetre.Axe, PerimetreId = s.AxeEntretien, NiveauAcces = NiveauAcces.Lecture,
                DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = lecteur.Id
            });
            await ctx.SaveChangesAsync();
        }
        s.Env.UtilisateurCourant.Utilisateur = lecteur;
        var restreint = (await s.Consommation().ConsoliderAsync(s.Filtre()))!;
        Assert.Equal(2_000_000, Assert.Single(restreint.Axes).Montants.DotationTotale);
        Assert.False(restreint.RecettesVisibles);

        // Sans habilitation : la lecture suit le profil, tout l'organisme.
        var direction = await s.Env.CreerUtilisateurAsync("direction", s.Organisme);
        s.Env.Droits.Accorder(direction.Id, TypeDroit.ConsommationLire);
        s.Env.UtilisateurCourant.Utilisateur = direction;
        Assert.Equal(3_000_000, (await s.Consommation().ConsoliderAsync(s.Filtre()))!.Total.DotationTotale);

        // Sans le droit : rien.
        var autre = await s.Env.CreerUtilisateurAsync("autre", s.Organisme);
        s.Env.UtilisateurCourant.Utilisateur = autre;
        Assert.Null(await s.Consommation().ConsoliderAsync(s.Filtre()));
    }

    // ── AE / CP, évolution, dérapages, tableau croisé ───────────────────────

    [Fact]
    public async Task AE_CP_programmes_executes_et_evolution_chronologique()
    {
        var s = await PreparerAsync();
        var engF = await EngagerAsync(s, s.LigneFonct, new(2027, 1, 15), 400_000);
        await EngagerAsync(s, s.LigneFonct, new(2027, 4, 15), 100_000);
        await PayerAsync(s, engF, new(2027, 2, 1), 250_000, "P1");

        var aecp = await s.Consommation().AECPParProgrammeAsync(s.Filtre());
        var a = aecp.Single(l => l.ProgrammeId == s.ProgrammeA);
        Assert.Equal(1_000_000, a.AEProgramme);                       // programmation de l'année 2027 seulement
        Assert.Equal(800_000, a.CPProgramme);
        Assert.Equal(500_000, a.AEExecute);
        Assert.Equal(250_000, a.CPExecute);
        Assert.Equal(50m, a.TauxAE);
        Assert.Equal(0, aecp.Single(l => l.ProgrammeId == s.ProgrammeB).AEExecute);

        var mensuel = await s.Consommation().EvolutionAsync(s.Filtre(), TypePeriode.Mois);
        Assert.Equal(12, mensuel.Count);
        Assert.Equal(400_000, mensuel[0].Engage);
        Assert.Equal(250_000, mensuel[1].Paye);
        Assert.Equal(500_000, mensuel[3].EngageCumule);
        Assert.Equal(3_000_000, mensuel[11].PrevisionCumulee);        // la dotation totale en fin d'exercice

        var trimestriel = await s.Consommation().EvolutionAsync(s.Filtre(), TypePeriode.Trimestre);
        Assert.Equal(4, trimestriel.Count);
        Assert.Equal(400_000, trimestriel[0].Engage);
        Assert.Equal(100_000, trimestriel[1].Engage);
        Assert.Equal("T1 2027", trimestriel[0].Libelle);
    }

    [Fact]
    public async Task Derapages_et_tableau_croise_programme_rubrique()
    {
        var s = await PreparerAsync();
        var engF = await EngagerAsync(s, s.LigneFonct, new(2027, 1, 10), 900_000);
        await PayerAsync(s, engF, new(2027, 2, 15), 600_000, "P1");

        // Fin mars : 600 000 payés en un quart d'année projettent ~2,4 M pour une dotation de 1 M.
        var filtre = s.Filtre();
        filtre.PeriodeId = await PeriodeAsync(s, TypePeriode.Trimestre, 1);
        var derapage = Assert.Single(await s.Consommation().DerapagesAsync(filtre));
        Assert.Equal(s.LigneFonct.Code, derapage.Dotation.LigneCode);
        Assert.Contains("projetés", derapage.Motif);
        Assert.True(derapage.TauxProjete > 100);

        var croise = (await s.Consommation().CroiserAsync(s.Filtre(), NiveauCroisement.ProgrammeRubrique))!;
        Assert.Equal(2, croise.Lignes.Count);
        Assert.Equal(2, croise.Colonnes.Count);
        var rubriqueFonct = croise.Colonnes.Single(c => c.Libelle.StartsWith("60")).Id;
        var rubriqueEntretien = croise.Colonnes.Single(c => c.Libelle.StartsWith("61")).Id;
        Assert.Equal(1_000_000, croise.Cellule(s.ProgrammeA, rubriqueFonct).DotationTotale);
        Assert.Equal(0, croise.Cellule(s.ProgrammeA, rubriqueEntretien).DotationTotale);
        Assert.Equal(900_000, croise.Cellule(s.ProgrammeA, rubriqueFonct).AETotal);

        var activiteLigne = (await s.Consommation().CroiserAsync(s.Filtre(), NiveauCroisement.ActiviteLigne))!;
        Assert.Equal(540_000, activiteLigne.Cellule(s.ActiviteA1, s.LigneFonct.Id).AETotal);   // 60 % de 900 000
    }

    // ── Tableau de bord ─────────────────────────────────────────────────────

    [Fact]
    public async Task Tableau_de_bord_widgets_alertes_et_cache()
    {
        var s = await PreparerAsync();
        var engF = await EngagerAsync(s, s.LigneFonct, new(2027, 1, 10), 950_000);   // 95 % : seuil de 90 %
        await PayerAsync(s, engF, new(2027, 2, 15), 300_000, "P1");

        var cache = new MemoryCache(new MemoryCacheOptions());
        var tdb = (await s.TableauDeBord(cache).ObtenirAsync(s.Filtre()))!;
        Assert.Equal(2027, tdb.Annee);
        Assert.Equal(3_000_000, tdb.Total.DotationTotale);
        Assert.Equal(["Travaux", "Investissement", "Fonctionnement", "Appuis"], tdb.Volets.Select(v => v.Volet));
        Assert.Equal(950_000, tdb.Volets.Single(v => v.Volet == "Fonctionnement").Montants.AETotal);
        Assert.Equal(0, tdb.Volets.Single(v => v.Volet == "Investissement").Montants.DotationTotale);

        var alerte = Assert.Single(tdb.Alertes);
        Assert.Equal(90m, alerte.Seuil);
        Assert.Equal("60 — Rubrique 60", Assert.Single(tdb.TopRubriques).Libelle);
        Assert.Equal(12, tdb.Evolution.Count);
        Assert.Equal(2, tdb.ParProgramme.Count);
        Assert.Equal([2027, 2028], tdb.Triennal.Select(t => t.Annee));
        Assert.Equal("2027-2029", tdb.Cycle);
        Assert.Equal(2, tdb.Performance.Count);

        // Mis en cache : une nouvelle pièce n'apparaît qu'en forçant le recalcul.
        await EngagerAsync(s, s.LigneEntretien, new(2027, 3, 1), 100_000);
        Assert.Same(tdb, await s.TableauDeBord(cache).ObtenirAsync(s.Filtre()));
        var frais = (await s.TableauDeBord(cache).ObtenirAsync(s.Filtre(), forcer: true))!;
        Assert.Equal(1_050_000, frais.Total.AETotal);

        // Sans le droit : rien.
        var autre = await s.Env.CreerUtilisateurAsync("autre", s.Organisme);
        s.Env.Droits.Accorder(autre.Id, TypeDroit.ConsommationLire);
        s.Env.UtilisateurCourant.Utilisateur = autre;
        Assert.Null(await s.TableauDeBord(cache).ObtenirAsync(s.Filtre()));
    }
}
