using ClosedXML.Excel;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Tests;

/// <summary>Tests clés du module Paramétrage (cahier technique § 4.2).</summary>
public class ParametrageTests
{
    /// <summary>Un gestionnaire du paramétrage, connecté dans l'organisme indiqué.</summary>
    private static async Task<Utilisateur> ConnecterGestionnaireAsync(Environnement env, Ulid organismeId, string login = "param")
    {
        var u = await env.CreerUtilisateurAsync(login, organismeId);
        env.Droits.Accorder(u.Id, TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer);
        env.UtilisateurCourant.Utilisateur = u;
        return u;
    }

    private static ReferentielService<T> Service<T>(Environnement env, Ulid organismeId) where T : EntiteOrganisme, new()
        => new(env.Fabrique(organismeId), new AccesParametrage(env.UtilisateurCourant, env.Droits), new AuditFactice());

    // ═════ Unicité des codes par organisme ════════════════════════════════════

    [Fact]
    public async Task Un_code_est_unique_dans_un_organisme_mais_pas_entre_organismes()
    {
        var env = new Environnement();
        var a = await env.CreerOrganismeAsync("A");
        var b = await env.CreerOrganismeAsync("B");

        await ConnecterGestionnaireAsync(env, a, "param-a");
        var axesA = Service<Axe>(env, a);
        Assert.True((await axesA.CreerAsync(new Axe { Code = "fonct", Libelle = "Fonctionnement", TypeAxe = TypeAxe.Depense, Section = Section.Fonctionnement })).Ok);

        // Même code, même organisme (et casse différente) : refusé.
        var doublon = await axesA.CreerAsync(new Axe { Code = "FONCT", Libelle = "Autre", TypeAxe = TypeAxe.Depense, Section = Section.Fonctionnement });
        Assert.False(doublon.Ok);
        Assert.Contains("déjà utilisé", doublon.Erreur);

        // Même code dans un autre organisme : admis.
        await ConnecterGestionnaireAsync(env, b, "param-b");
        Assert.True((await Service<Axe>(env, b).CreerAsync(new Axe { Code = "FONCT", Libelle = "Fonctionnement", TypeAxe = TypeAxe.Depense, Section = Section.Fonctionnement })).Ok);
    }

    [Fact]
    public async Task Un_code_supprime_peut_etre_reutilise()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        var service = Service<CategorieEmploi>(env, org);

        var (_, _, cree) = await service.CreerAsync(new CategorieEmploi { Code = "HC", Libelle = "Hors catégorie" });
        Assert.True((await service.SupprimerAsync(cree!.Id)).Ok);
        Assert.True((await service.CreerAsync(new CategorieEmploi { Code = "HC", Libelle = "Hors catégorie" })).Ok);
    }

    // ═════ Désactivation et suppression d'un élément utilisé ═════════════════

    [Fact]
    public async Task Desactivation_refusee_tant_qu_un_element_actif_en_depend()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);

        var axe = (await Service<Axe>(env, org).CreerAsync(new Axe { Code = "INV", Libelle = "Investissements", TypeAxe = TypeAxe.Depense, Section = Section.Investissement })).Element!;
        var rubriques = Service<Rubrique>(env, org);
        var rubrique = (await rubriques.CreerAsync(new Rubrique { AxeId = axe.Id, Code = "21", Libelle = "Immobilisations" })).Element!;
        var sousRubriques = Service<SousRubrique>(env, org);
        var sousRubrique = (await sousRubriques.CreerAsync(new SousRubrique { RubriqueId = rubrique.Id, Code = "211", Libelle = "Terrains" })).Element!;

        var refus = await rubriques.ChangerEtatAsync(rubrique.Id, false);
        Assert.False(refus.Ok);
        Assert.Contains("sous-rubriques", refus.Erreur);

        // La sous-rubrique désactivée ne bloque plus la désactivation…
        Assert.True((await sousRubriques.ChangerEtatAsync(sousRubrique.Id, false)).Ok);
        Assert.True((await rubriques.ChangerEtatAsync(rubrique.Id, false)).Ok);

        // … mais elle bloque toujours la suppression : elle existe encore.
        var suppression = await rubriques.SupprimerAsync(rubrique.Id);
        Assert.False(suppression.Ok);
        Assert.Contains("Suppression impossible", suppression.Erreur);
    }

    [Fact]
    public async Task La_suppression_d_une_ligne_emporte_ses_correspondances_de_comptes()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        await using (var ctx = env.Contexte(org))
        {
            await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
            await ctx.SaveChangesAsync();
        }

        Ulid sousRubriqueId, sourceId, natureId, compteId;
        await using (var ctx = env.Contexte(org))
        {
            var axe = await ctx.Axes.FirstAsync(a => a.Code == "FONCTIONNEMENT");
            var rubrique = new Rubrique { AxeId = axe.Id, Code = "60", Libelle = "Achats" };
            var sr = new SousRubrique { Rubrique = rubrique, Code = "601", Libelle = "Fournitures" };
            var compte = new CompteComptable { Numero = "6011", Libelle = "Fournitures", Classe = 6 };
            ctx.AddRange(rubrique, sr, compte);
            await ctx.SaveChangesAsync();
            (sousRubriqueId, compteId) = (sr.Id, compte.Id);
            sourceId = (await ctx.SourcesFinancement.FirstAsync(s => s.Code == "RI")).Id;
            natureId = (await ctx.NaturesDepense.FirstAsync(n => n.Code == "2")).Id;
        }

        var lignes = Service<LigneBudgetaire>(env, org);
        var ligne = (await lignes.CreerAsync(new LigneBudgetaire
        {
            SousRubriqueId = sousRubriqueId, Code = "60110", Libelle = "Fournitures de bureau", Sens = SensLigne.Depense,
            NatureDepenseId = natureId, SourceFinancementId = sourceId
        })).Element!;

        var nomenclature = new NomenclatureService(env.Fabrique(org), new AccesParametrage(env.UtilisateurCourant, env.Droits), new AuditFactice());
        Assert.True((await nomenclature.DefinirComptesLigneAsync(ligne.Id, [compteId], null)).Ok);

        // Le compte, lui, est utilisé par la ligne : il ne se supprime pas.
        Assert.False((await Service<CompteComptable>(env, org).SupprimerAsync(compteId)).Ok);

        // La ligne se supprime, avec ses correspondances.
        Assert.True((await lignes.SupprimerAsync(ligne.Id)).Ok);
        await using var verif = env.Contexte(org);
        Assert.Empty(await verif.LignesComptes.ToListAsync());
    }

    [Fact]
    public async Task Le_sens_d_une_ligne_suit_le_type_de_son_axe()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        await using (var ctx = env.Contexte(org))
        {
            await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
            await ctx.SaveChangesAsync();
        }

        await using var c = env.Contexte(org);
        var axeRecettes = await c.Axes.FirstAsync(a => a.Code == "RECETTES");
        var rubrique = new Rubrique { AxeId = axeRecettes.Id, Code = "70", Libelle = "Recettes propres" };
        var sr = new SousRubrique { Rubrique = rubrique, Code = "701", Libelle = "Péage" };
        c.AddRange(rubrique, sr);
        await c.SaveChangesAsync();

        var resultat = await Service<LigneBudgetaire>(env, org).CreerAsync(new LigneBudgetaire
        {
            SousRubriqueId = sr.Id, Code = "70101", Libelle = "Péage", Sens = SensLigne.Depense,
            NatureDepenseId = (await c.NaturesDepense.FirstAsync()).Id,
            SourceFinancementId = (await c.SourcesFinancement.FirstAsync()).Id
        });

        Assert.False(resultat.Ok);
        Assert.Contains("Recette", resultat.Erreur);
    }

    // ═════ Exercices et périodes ══════════════════════════════════════════════

    [Fact]
    public void Generation_des_19_periodes_d_un_exercice()
    {
        var periodes = ExerciceService.GenererPeriodes(2028);

        Assert.Equal(19, periodes.Count);
        Assert.Equal(12, periodes.Count(p => p.TypePeriode == TypePeriode.Mois));
        Assert.Equal(4, periodes.Count(p => p.TypePeriode == TypePeriode.Trimestre));
        Assert.Equal(2, periodes.Count(p => p.TypePeriode == TypePeriode.Semestre));

        // Année bissextile : février se termine le 29.
        Assert.Equal(new DateOnly(2028, 2, 29), periodes.Single(p => p.TypePeriode == TypePeriode.Mois && p.Numero == 2).DateFin);
        Assert.Equal((new DateOnly(2028, 10, 1), new DateOnly(2028, 12, 31)),
            periodes.Where(p => p.TypePeriode == TypePeriode.Trimestre && p.Numero == 4).Select(p => (p.DateDebut, p.DateFin)).Single());
        Assert.Equal((new DateOnly(2028, 7, 1), new DateOnly(2028, 12, 31)),
            periodes.Where(p => p.TypePeriode == TypePeriode.Semestre && p.Numero == 2).Select(p => (p.DateDebut, p.DateFin)).Single());
        Assert.Equal((new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31)),
            periodes.Where(p => p.TypePeriode == TypePeriode.Annee).Select(p => (p.DateDebut, p.DateFin)).Single());

        // Les mois couvrent l'année sans trou ni chevauchement.
        var mois = periodes.Where(p => p.TypePeriode == TypePeriode.Mois).OrderBy(p => p.Numero).ToList();
        for (var i = 1; i < 12; i++) Assert.Equal(mois[i - 1].DateFin.AddDays(1), mois[i].DateDebut);
    }

    [Fact]
    public async Task Creation_d_un_exercice_avec_ses_periodes_et_refus_d_un_doublon()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        var service = new ExerciceService(env.Fabrique(org), new AccesParametrage(env.UtilisateurCourant, env.Droits), new AuditFactice());

        Assert.True((await service.CreerAsync(2027)).Ok);
        Assert.False((await service.CreerAsync(2027)).Ok);

        var exercice = Assert.Single(await service.ListerAsync());
        Assert.Equal(StatutExercice.EnPreparation, exercice.Statut);
        Assert.Equal(19, (await service.ListerPeriodesAsync(exercice.Id)).Count);

        // Une période ne se clôt que sur un exercice ouvert.
        var janvier = (await service.ListerPeriodesAsync(exercice.Id)).First(p => p.Type == TypePeriode.Mois && p.Numero == 1);
        Assert.False((await service.ChangerCloturePeriodeAsync(janvier.Id, true)).Ok);
        Assert.True((await service.OuvrirAsync(exercice.Id)).Ok);
        Assert.True((await service.ChangerCloturePeriodeAsync(janvier.Id, true)).Ok);
    }

    // ═════ Import Excel ═══════════════════════════════════════════════════════

    private static MemoryStream Classeur(params string?[][] lignes)
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.Worksheets.Add("Axes");
        for (var l = 0; l < lignes.Length; l++)
            for (var c = 0; c < lignes[l].Length; c++)
                feuille.Cell(l + 1, c + 1).Value = lignes[l][c] ?? "";
        var flux = new MemoryStream();
        classeur.SaveAs(flux);
        flux.Position = 0;
        return flux;
    }

    [Fact]
    public async Task Import_en_erreur_rapport_ligne_par_ligne_et_rien_n_est_enregistre()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        var service = Service<Axe>(env, org);

        using var fichier = Classeur(
            ["Code *", "Libellé *", "Type *", "Section *", "Ordre", "Actif"],
            ["ENT", "Entretien routier", "Dépense", "Investissement", "1", "Oui"],      // ligne 2 : correcte
            ["FCT", "", "Dépense", "Fonctionnement", "2", "Oui"],                       // ligne 3 : libellé manquant
            ["REC", "Recettes", "Produit", "Fonctionnement", "3", "Oui"],               // ligne 4 : type inconnu
            ["ENT", "Doublon", "Dépense", "Investissement", "abc", "Oui"]);             // ligne 5 : ordre invalide, code en double

        var rapport = await service.ImporterAsync(fichier);

        Assert.False(rapport.Ok);
        Assert.Contains(rapport.Erreurs, e => e.Ligne == 3 && e.Colonne == "Libellé");
        Assert.Contains(rapport.Erreurs, e => e.Ligne == 4 && e.Colonne == "Type");
        Assert.Contains(rapport.Erreurs, e => e.Ligne == 5 && e.Colonne == "Ordre");
        Assert.DoesNotContain(rapport.Erreurs, e => e.Ligne == 2);

        // Tout ou rien : même la ligne correcte n'a pas été enregistrée.
        await using var ctx = env.Contexte(org);
        Assert.Empty(await ctx.Axes.ToListAsync());
    }

    [Fact]
    public async Task Import_valide_cree_et_met_a_jour_puis_l_export_se_reimporte()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        var service = Service<Axe>(env, org);
        await service.CreerAsync(new Axe { Code = "ENT", Libelle = "Ancien libellé", TypeAxe = TypeAxe.Depense, Section = Section.Investissement });

        using var fichier = Classeur(
            ["Code", "Libellé", "Type", "Section", "Ordre"],
            ["ent", "Entretien routier", "Dépense", "Investissement", "1"],
            ["REC", "Recettes", "Recette", "Fonctionnement", "5"]);

        var rapport = await service.ImporterAsync(fichier);

        Assert.True(rapport.Ok, string.Join(" | ", rapport.Erreurs.Select(e => $"{e.Ligne}:{e.Colonne}:{e.Message}")));
        Assert.Equal((1, 1), (rapport.NbCrees, rapport.NbModifies));

        await using (var ctx = env.Contexte(org))
            Assert.Equal("Entretien routier", (await ctx.Axes.SingleAsync(a => a.Code == "ENT")).Libelle);

        // L'export sert de modèle : réimporté tel quel, il ne crée rien et met tout à jour.
        using var export = new MemoryStream((await service.ExporterAsync())!);
        var reimport = await service.ImporterAsync(export);
        Assert.True(reimport.Ok);
        Assert.Equal((0, 2), (reimport.NbCrees, reimport.NbModifies));
    }

    [Fact]
    public async Task Import_refuse_une_colonne_obligatoire_absente()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);

        using var fichier = Classeur(["Code", "Type", "Section"], ["ENT", "Dépense", "Investissement"]);
        var rapport = await Service<Axe>(env, org).ImporterAsync(fichier);

        Assert.Contains(rapport.Erreurs, e => e.Ligne == 1 && e.Colonne == "Libellé");
    }

    // ═════ Droits et données initiales ════════════════════════════════════════

    [Fact]
    public async Task Sans_le_droit_de_gestion_le_service_refuse_les_ecritures()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var lecteur = await env.CreerUtilisateurAsync("lecteur", org);
        env.Droits.Accorder(lecteur.Id, TypeDroit.ParametrageLire);
        env.UtilisateurCourant.Utilisateur = lecteur;

        var resultat = await Service<Axe>(env, org).CreerAsync(new Axe { Code = "X", Libelle = "X", TypeAxe = TypeAxe.Depense, Section = Section.Fonctionnement });
        Assert.False(resultat.Ok);
    }

    [Fact]
    public async Task Les_donnees_initiales_se_completent_sans_doublon()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");

        int premier, second;
        await using (var ctx = env.Contexte(org))
        {
            premier = await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = env.Contexte(org))
        {
            second = await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
            await ctx.SaveChangesAsync();
        }

        Assert.True(premier > 50);
        Assert.Equal(0, second);

        await using var verif = env.Contexte(org);
        // Quatre types de travaux (C.2.11) et leurs prestations de contrôle.
        Assert.Equal(8, await verif.TypesTravaux.CountAsync());
        Assert.True(await verif.TypesTravaux.Where(t => t.EstPrestationControle).AllAsync(t => t.TypeTravauxControleId != null));
        Assert.Equal(5, await verif.Structures.CountAsync(s => s.StructureParentId != null || s.Code == "DG"));
    }

    // ═════ Réponses du métier : collectivités (C.2.8), historique (C.2.10) ════

    [Fact]
    public async Task Une_collectivite_n_est_rattachee_qu_a_un_seul_poste()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        Ulid nord, sud, communeA, communeB;
        await using (var ctx = env.Contexte(org))
        {
            var n = new Site { Code = "PN", Nom = "Péage nord", TypeSite = TypeSite.PostePeage };
            var s = new Site { Code = "PS", Nom = "Péage sud", TypeSite = TypeSite.PostePeage };
            var a = new Tiers { Code = "CA", RaisonSociale = "Commune A", TypeTiers = TypeTiers.EntiteBeneficiaireAppuis };
            var b = new Tiers { Code = "CB", RaisonSociale = "Commune B", TypeTiers = TypeTiers.EntiteBeneficiaireAppuis };
            ctx.AddRange(n, s, a, b);
            await ctx.SaveChangesAsync();
            (nord, sud, communeA, communeB) = (n.Id, s.Id, a.Id, b.Id);
        }
        var nomenclature = new NomenclatureService(env.Fabrique(org), new AccesParametrage(env.UtilisateurCourant, env.Droits), new AuditFactice());

        // Plusieurs collectivités pour un poste…
        Assert.True((await nomenclature.DefinirCollectivitesSiteAsync(nord, [communeA, communeB])).Ok);
        // … mais une collectivité déjà rattachée ne l'est pas à un second poste.
        var refus = await nomenclature.DefinirCollectivitesSiteAsync(sud, [communeA]);
        Assert.False(refus.Ok);
        Assert.Contains("Péage nord", refus.Erreur);
        Assert.Equal("Péage nord", (await nomenclature.CollectivitesRattacheesAilleursAsync(sud))[communeA]);

        // Libérée du premier poste, elle se rattache au second.
        Assert.True((await nomenclature.DefinirCollectivitesSiteAsync(nord, [communeB])).Ok);
        Assert.True((await nomenclature.DefinirCollectivitesSiteAsync(sud, [communeA])).Ok);
    }

    [Fact]
    public async Task Reprise_de_l_historique_sur_cinq_exercices_tout_ou_rien()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        await ConnecterGestionnaireAsync(env, org);
        await using (var ctx = env.Contexte(org))
        {
            await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
            ctx.Exercices.Add(new Exercice { Annee = 2027, DateDebut = new(2027, 1, 1), DateFin = new(2027, 12, 31) });
            ctx.Sites.Add(new Site { Code = "P1", Nom = "Péage", TypeSite = TypeSite.PostePeage });
            await ctx.SaveChangesAsync();
        }
        var service = new HistoriqueService(env.Fabrique(org), new AccesParametrage(env.UtilisateurCourant, env.Droits), new AuditFactice());
        Assert.Equal((2022, 2026), await service.AnneesAdmisesAsync());

        static MemoryStream Classeur(params object?[][] lignes)
        {
            using var classeur = new XLWorkbook();
            var f = classeur.AddWorksheet("Historique");
            string[] entetes = ["Année", "Axe", "Rubrique", "Nature de dépense", "Nature de recette", "Site",
                                "Prévision", "Montant autorisé", "Montant réalisé", "Restant à payer", "Km entretenus"];
            for (var i = 0; i < entetes.Length; i++) f.Cell(1, i + 1).Value = entetes[i];
            for (var r = 0; r < lignes.Length; r++)
                for (var c = 0; c < lignes[r].Length; c++)
                    if (lignes[r][c] is { } v) f.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(v);
            var flux = new MemoryStream();
            classeur.SaveAs(flux);
            flux.Position = 0;
            return flux;
        }

        // Une année hors des cinq exercices, un code inconnu : rien n'est enregistré.
        var refus = await service.ImporterAsync(Classeur(
            [2026, "ENTRETIEN", null, null, null, null, 1_000_000, 900_000, 800_000, 100_000, 120],
            [2019, "ENTRETIEN", null, null, null, null, 1, null, null, null, null],
            [2025, "INCONNU", null, null, null, null, 1, null, null, null, null]));
        Assert.False(refus.Ok);
        Assert.Equal(2, refus.Erreurs.Count);
        Assert.Empty(await service.SyntheseAsync());

        Assert.True((await service.ImporterAsync(Classeur(
            [2026, "ENTRETIEN", null, null, null, null, 1_000_000, 900_000, 800_000, 100_000, 120],
            [2026, null, null, null, "RP_PEAGE", "P1", 500_000, null, 450_000, null, null]))).Ok);
        // Réimporter une année remplace ses lignes importées.
        var remplace = await service.ImporterAsync(Classeur([2026, "ENTRETIEN", null, null, null, null, 2_000_000, null, 1_500_000, null, 200]));
        Assert.True(remplace.Ok);
        Assert.Equal(2, remplace.NbModifies);
        var annee = Assert.Single(await service.SyntheseAsync());
        Assert.Equal((2026, SourceHistorique.Import, 1, 2_000_000m), (annee.Annee, annee.Source, annee.NbLignes, annee.Prevision));

        Assert.NotNull(await service.ModeleAsync());
        Assert.True((await service.SupprimerAnneeAsync(2026)).Ok);
        Assert.Empty(await service.SyntheseAsync());
    }
}
