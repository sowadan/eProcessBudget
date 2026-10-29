using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.BudgetProgramme;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Tests;

/// <summary>Tests clés du module Budget-programme et performance (cahier technique § 4.3).</summary>
public class BudgetProgrammeTests
{
    // ═════ Calculs purs ═══════════════════════════════════════════════════════

    [Fact]
    public void Taux_d_atteinte_d_un_indicateur_a_la_baisse()
    {
        // Délai moyen de traitement : cible 10 jours.
        Assert.Equal(125m, CalculsBudgetProgramme.TauxAtteinte(8, 10, SensEvolution.BaisseAttendue));   // mieux que la cible
        Assert.Equal(80m, CalculsBudgetProgramme.TauxAtteinte(12.5m, 10, SensEvolution.BaisseAttendue)); // moins bien
        Assert.True(CalculsBudgetProgramme.EstAtteinte(8, 10, SensEvolution.BaisseAttendue));
        Assert.False(CalculsBudgetProgramme.EstAtteinte(12.5m, 10, SensEvolution.BaisseAttendue));

        // À la hausse, la formule est inversée.
        Assert.Equal(80m, CalculsBudgetProgramme.TauxAtteinte(8, 10, SensEvolution.HausseAttendue));

        // Dénominateur nul ou cible absente : non significatif.
        Assert.Null(CalculsBudgetProgramme.TauxAtteinte(0, 10, SensEvolution.BaisseAttendue));
        Assert.Null(CalculsBudgetProgramme.TauxAtteinte(5, null, SensEvolution.HausseAttendue));
    }

    [Fact]
    public void Ventilation_des_charges_de_personnel_selon_le_temps()
    {
        // Trois actions, temps cumulés des agents : 150 %, 100 %, 50 % (total 300).
        var parts = CalculsBudgetProgramme.Ventiler(1_000_000m, [("A1", 150m), ("A2", 100m), ("A3", 50m)]);

        Assert.Equal(500_000m, parts.Single(p => p.Cle == "A1").Montant);
        Assert.Equal(333_333.33m, parts.Single(p => p.Cle == "A2").Montant);
        Assert.Equal(166_666.67m, parts.Single(p => p.Cle == "A3").Montant);
        Assert.Equal(1_000_000m, parts.Sum(p => p.Montant));   // l'arrondi ne perd rien

        // Arrondi : l'écart va à la plus grosse part, la somme reste exacte.
        var tiers = CalculsBudgetProgramme.Ventiler(100m, [("X", 1m), ("Y", 1m), ("Z", 1m)]);
        Assert.Equal(100m, tiers.Sum(p => p.Montant));
    }

    [Fact]
    public void La_somme_des_cles_de_repartition_doit_faire_100()
    {
        var a = Ulid.NewUlid(); var b = Ulid.NewUlid();
        Assert.Null(RepartitionService.ControlerCles([(a, 60m), (b, 40m)]));
        Assert.Null(RepartitionService.ControlerCles([]));                          // aucune répartition
        Assert.Contains("100 %", RepartitionService.ControlerCles([(a, 60m), (b, 39.9999m)]));
        Assert.Contains("100 %", RepartitionService.ControlerCles([(a, 60m), (b, 50m)]));
        Assert.NotNull(RepartitionService.ControlerCles([(a, 50m), (a, 50m)]));     // activité en double
        Assert.NotNull(RepartitionService.ControlerCles([(a, 0m), (b, 100m)]));     // part nulle
    }

    [Fact]
    public void Les_CP_cumules_ne_depassent_pas_les_AE_cumulees()
    {
        int[] annees = [2026, 2027, 2028, 2029];
        // AE pluriannuelle en 2027, CP étalés : admis.
        Assert.Null(ProgrammationService.ControlerAECP(new Dictionary<int, MontantsAECP>
        {
            [2027] = new(300, 100), [2028] = new(0, 100), [2029] = new(0, 100)
        }, annees));
        // CP 2028 au-delà des AE cumulées : refusé.
        Assert.Contains("2028", ProgrammationService.ControlerAECP(new Dictionary<int, MontantsAECP>
        {
            [2027] = new(100, 50), [2028] = new(0, 100)
        }, annees));
    }

    // ═════ Services sur base en mémoire ═══════════════════════════════════════

    private sealed record Scene(Environnement Env, Ulid Organisme, Utilisateur Responsable, Programme Programme, ActionProgramme Action,
                                Activite Activite1, Activite Activite2, LigneBudgetaire Ligne, Ulid ExerciceId);

    /// <summary>Un organisme provisionné, un programme P1 avec une action et deux activités, une ligne de dépense, l'exercice 2027 ouvert.</summary>
    private static async Task<Scene> PreparerAsync()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var responsable = await env.CreerUtilisateurAsync("budget", org);
        env.Droits.Accorder(responsable.Id, TypeDroit.ProgrammationSaisir, TypeDroit.BudgetProgrammeDefinir,
            TypeDroit.IndicateurSaisirRealisation, TypeDroit.ParametrageGerer, TypeDroit.ParametrageLire);
        env.UtilisateurCourant.Utilisateur = responsable;

        await using var ctx = env.Contexte(org);
        await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
        await ctx.SaveChangesAsync();

        var programme = new Programme { Code = "P1", Libelle = "Pilotage", ObjectifGeneral = "Piloter", ResponsableId = responsable.Id };
        var action = new ActionProgramme { Programme = programme, Code = "1.1", Libelle = "Coordination", ObjectifSpecifique = "Coordonner" };
        var a1 = new Activite { Action = action, Code = "1.1.1", Libelle = "Réunions" };
        var a2 = new Activite { Action = action, Code = "1.1.2", Libelle = "Rapports" };
        var axe = await ctx.Axes.FirstAsync(a => a.Code == "FONCTIONNEMENT");
        var rubrique = new Rubrique { AxeId = axe.Id, Code = "60", Libelle = "Achats" };
        var sr = new SousRubrique { Rubrique = rubrique, Code = "601", Libelle = "Fournitures" };
        var ligne = new LigneBudgetaire
        {
            SousRubrique = sr, Code = "60110", Libelle = "Fournitures", Sens = SensLigne.Depense,
            NatureDepenseId = (await ctx.NaturesDepense.FirstAsync(n => n.Code == "2")).Id,
            SourceFinancementId = (await ctx.SourcesFinancement.FirstAsync(s => s.Code == "RI")).Id
        };
        var exercice = new Exercice { Annee = 2027, DateDebut = new(2027, 1, 1), DateFin = new(2027, 12, 31), Statut = StatutExercice.Ouvert };
        ctx.AddRange(programme, action, a1, a2, rubrique, sr, ligne, exercice);
        await ctx.SaveChangesAsync();

        return new Scene(env, org, responsable, programme, action, a1, a2, ligne, exercice.Id);
    }

    private static AccesBudgetProgramme Acces(Scene s)
        => new(new AccesParametrage(s.Env.UtilisateurCourant, s.Env.Droits), new ControlePerimetre(s.Env.Fabrique(s.Organisme)));

    /// <summary>Habilitation de saisie active, comme après la double validation.</summary>
    private static async Task HabiliterAsync(Scene s, TypePerimetre type, Ulid? perimetreId)
    {
        await using var ctx = s.Env.Contexte(s.Organisme);
        ctx.Habilitations.Add(new Habilitation
        {
            UtilisateurId = s.Responsable.Id, TypePerimetre = type, PerimetreId = perimetreId, NiveauAcces = NiveauAcces.Saisie,
            DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = s.Responsable.Id
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Cles_de_repartition_refusees_hors_perimetre_puis_admises_a_100_pour_cent()
    {
        var s = await PreparerAsync();
        var service = new RepartitionService(s.Env.Fabrique(s.Organisme), Acces(s), new AuditFactice());
        var parts = new List<(Ulid, decimal)> { (s.Activite1.Id, 70m), (s.Activite2.Id, 30m) };

        // Sans habilitation de saisie : refus, même avec le droit.
        var refus = await service.DefinirClesAsync(s.ExerciceId, s.Ligne.Id, parts);
        Assert.False(refus.Ok);
        Assert.Contains("habilitation", refus.Erreur);

        await HabiliterAsync(s, TypePerimetre.Programme, s.Programme.Id);

        Assert.False((await service.DefinirClesAsync(s.ExerciceId, s.Ligne.Id, [(s.Activite1.Id, 70m), (s.Activite2.Id, 20m)])).Ok);
        Assert.True((await service.DefinirClesAsync(s.ExerciceId, s.Ligne.Id, parts)).Ok);

        var cles = await service.ObtenirClesAsync(s.ExerciceId, s.Ligne.Id);
        Assert.Equal(100m, cles.Sum(c => c.Pourcentage));
    }

    [Fact]
    public async Task Reconduction_d_un_cycle_reprend_les_projections_et_les_cibles()
    {
        var s = await PreparerAsync();
        await HabiliterAsync(s, TypePerimetre.OrganismeEntier, null);
        var acces = Acces(s);
        var cycles = new CycleProgrammationService(s.Env.Fabrique(s.Organisme), new AccesParametrage(s.Env.UtilisateurCourant, s.Env.Droits), new AuditFactice());
        var programmation = new ProgrammationService(s.Env.Fabrique(s.Organisme), acces, new AuditFactice());
        var indicateurs = new IndicateurService(s.Env.Fabrique(s.Organisme), acces, new AuditFactice());

        Assert.True((await cycles.CreerAsync(2027, null)).Ok);
        var cycle = Assert.Single(await cycles.ListerAsync());

        Ulid natureId, sourceId, uniteId;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            natureId = (await ctx.NaturesDepense.FirstAsync(n => n.Code == "2")).Id;
            sourceId = (await ctx.SourcesFinancement.FirstAsync(n => n.Code == "RI")).Id;
            uniteId = (await ctx.UnitesMesure.FirstAsync(n => n.Code == "POURCENT")).Id;
        }

        // AE/CP 2026 à 2029 sur le cycle 2027-2029.
        Assert.True((await programmation.EnregistrerFinancierAsync(new SaisieProgrammationFinanciere
        {
            CycleId = cycle.Id, ActionId = s.Action.Id, NatureDepenseId = natureId, SourceFinancementId = sourceId,
            Montants = { [2026] = new(100, 100), [2027] = new(200, 200), [2028] = new(300, 300), [2029] = new(400, 400) }
        })).Ok);

        // Un indicateur complet (référence + cibles), sinon le cycle ne se valide pas.
        var (_, _, indicateur) = await new ReferentielService<Indicateur>(s.Env.Fabrique(s.Organisme),
                new AccesParametrage(s.Env.UtilisateurCourant, s.Env.Droits), new AuditFactice())
            .CreerAsync(new Indicateur { Niveau = NiveauIndicateur.Programme, ProgrammeId = s.Programme.Id, Code = "IND1", Libelle = "Taux d'exécution",
                                          UniteMesureId = uniteId, AnneeReference = 2026, ValeurReference = 70 });
        Assert.NotNull(indicateur);
        Assert.False((await cycles.ValiderAsync(cycle.Id)).Ok);   // cibles manquantes
        Assert.True((await indicateurs.DefinirCiblesAsync(indicateur!.Id, cycle.Id,
            new Dictionary<int, decimal?> { [2027] = 75, [2028] = 80, [2029] = 85 })).Ok);
        Assert.True((await cycles.ValiderAsync(cycle.Id)).Ok);

        // Un cycle validé n'est plus modifiable…
        Assert.False((await indicateurs.DefinirCiblesAsync(indicateur.Id, cycle.Id, new Dictionary<int, decimal?> { [2027] = 1 })).Ok);

        // … mais se reconduit : 2028-2030, base de travail reprise.
        Assert.True((await cycles.ReconduireAsync(cycle.Id)).Ok);
        var nouveau = (await cycles.ListerAsync()).Single(c => c.AnneeBase == 2028);
        Assert.Equal(StatutCycle.Brouillon, nouveau.Statut);

        var lignes = await programmation.ListerFinancierAsync(nouveau.Id, s.Programme.Id);
        var montants = Assert.Single(lignes).Montants;
        Assert.Equal([2027, 2028, 2029], montants.Keys.Order());   // 2026 hors du nouveau cycle, 2030 à saisir
        Assert.Equal(300m, montants[2028].AE);

        var cibles = await indicateurs.ObtenirCiblesAsync(indicateur.Id, nouveau.Id);
        Assert.Equal(new Dictionary<int, decimal> { [2028] = 80, [2029] = 85 }, cibles);
    }

    [Fact]
    public async Task Realisation_hors_cible_exige_une_justification_et_se_valide()
    {
        var s = await PreparerAsync();
        await HabiliterAsync(s, TypePerimetre.Programme, s.Programme.Id);
        var acces = Acces(s);
        var service = new IndicateurService(s.Env.Fabrique(s.Organisme), acces, new AuditFactice());

        Ulid indicateurId, periodeId;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var cycle = new CycleProgrammation { Libelle = "C", AnneeReference = 2026, AnneeBase = 2027, AnneeFin = 2029 };
            var indicateur = new Indicateur
            {
                Niveau = NiveauIndicateur.Programme, ProgrammeId = s.Programme.Id, Code = "DELAI", Libelle = "Délai moyen",
                UniteMesureId = (await ctx.UnitesMesure.FirstAsync(u => u.Code == "JOURS")).Id, SensEvolution = SensEvolution.BaisseAttendue
            };
            var periode = new Periode { ExerciceId = s.ExerciceId, TypePeriode = TypePeriode.Trimestre, Numero = 1, DateDebut = new(2027, 1, 1), DateFin = new(2027, 3, 31) };
            ctx.AddRange(cycle, indicateur, periode,
                new IndicateurCible { Indicateur = indicateur, CycleProgrammation = cycle, Annee = 2027, ValeurCible = 10 });
            await ctx.SaveChangesAsync();
            (indicateurId, periodeId) = (indicateur.Id, periode.Id);
        }

        // 12 jours pour une cible de 10, à la baisse : non atteinte, justification exigée.
        var sansJustification = await service.EnregistrerRealisationAsync(new SaisieRealisation { IndicateurId = indicateurId, PeriodeId = periodeId, Valeur = 12 });
        Assert.False(sansJustification.Ok);
        Assert.True((await service.EnregistrerRealisationAsync(new SaisieRealisation
        {
            IndicateurId = indicateurId, PeriodeId = periodeId, Valeur = 12, Justification = "Effectif réduit au premier trimestre"
        })).Ok);

        var ligne = Assert.Single(await service.ListerRealisationsAsync(periodeId));
        Assert.Equal(83.33m, ligne.TauxAtteinte);
        Assert.False(ligne.EstAtteinte);

        Assert.True((await service.ValiderRealisationAsync(ligne.RealisationId!.Value)).Ok);
        var modification = await service.EnregistrerRealisationAsync(new SaisieRealisation { IndicateurId = indicateurId, PeriodeId = periodeId, Valeur = 9 });
        Assert.False(modification.Ok);   // validée : à rouvrir d'abord
    }

    [Fact]
    public async Task Ventilation_calculee_depuis_le_temps_des_agents()
    {
        var s = await PreparerAsync();
        await HabiliterAsync(s, TypePerimetre.OrganismeEntier, null);
        var service = new RepartitionService(s.Env.Fabrique(s.Organisme), Acces(s), new AuditFactice());

        Ulid action2Id, agent1, agent2;
        await using (var ctx = s.Env.Contexte(s.Organisme))
        {
            var action2 = new ActionProgramme { ProgrammeId = s.Programme.Id, Code = "1.2", Libelle = "RH", ObjectifSpecifique = "RH" };
            var categorie = await ctx.CategoriesEmploi.FirstAsync();
            var structure = await ctx.Structures.FirstAsync();
            var a1 = new Agent { Matricule = "A1", NomComplet = "Agent 1", CategorieEmploiId = categorie.Id, StructureId = structure.Id };
            var a2 = new Agent { Matricule = "A2", NomComplet = "Agent 2", CategorieEmploiId = categorie.Id, StructureId = structure.Id };
            ctx.AddRange(action2, a1, a2);
            await ctx.SaveChangesAsync();
            (action2Id, agent1, agent2) = (action2.Id, a1.Id, a2.Id);
        }

        // Agent 1 : 100 % sur 1.1 ; agent 2 : 50 % / 50 %. Poids : 1.1 = 150, 1.2 = 50.
        Assert.True((await service.DefinirTempsAsync(s.ExerciceId, agent1, [(s.Action.Id, null, 100m)])).Ok);
        Assert.True((await service.DefinirTempsAsync(s.ExerciceId, agent2, [(s.Action.Id, null, 50m), (action2Id, null, 50m)])).Ok);
        Assert.False((await service.DefinirTempsAsync(s.ExerciceId, agent2, [(s.Action.Id, null, 80m), (action2Id, null, 30m)])).Ok); // > 100 %

        var ventilation = await service.VentilerAsync(s.ExerciceId, 800_000m, 200_000m);
        var a11 = ventilation.Single(v => v.Action == "1.1");
        Assert.Equal(75m, a11.PartTemps);
        Assert.Equal(600_000m, a11.Personnel);
        Assert.Equal(150_000m, a11.Communes);
        Assert.Equal(1_000_000m, ventilation.Sum(v => v.Total));
    }
}
