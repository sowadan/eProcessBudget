using ClosedXML.Excel;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.BudgetProgramme;
using eProcessBudget.Infrastructure.Services.Consommation;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Infrastructure.Services.Rapports;
using eProcessBudget.Infrastructure.Services.Recettes;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace eProcessBudget.Tests;

/// <summary>Tests clés des rapports et de l'exportation (cahier technique § 2.10 et 4.8).</summary>
public class RapportsTests
{
    static RapportsTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static readonly EnteteInstitutionnelle Entete = new("Fonds routier", "FR", "Lomé", "+228 00 00 00 00", null, "Direction générale", "Document interne", "FCFA");

    private static Rapport Exemple() => new("Situation budgétaire par axe", null, [("Exercice", "2027"), ("Axe", "Fonctionnement")],
    [
        new("Chiffres clés", Chiffres: [("Dotation totale", "1 500,00")]),
        new("Par rubrique", new TableauRapport(
            [new("Rubrique"), new("Dotation", TypeColonne.Montant), new("Taux", TypeColonne.Taux)],
            [new(["60 — Achats", 1_000m, 50m]), new(["61 — Services", 500m, null])],
            new(["Total", 1_500m, 33.33m]))),
        new("Commentaire", Texte: "Exécution conforme."),
    ]);

    // ═════ Rendus ═════════════════════════════════════════════════════════════

    [Fact]
    public void Rendu_Excel_en_tete_filtres_formats_et_formule_de_total()
    {
        using var classeur = new XLWorkbook(new MemoryStream(RenduExcel.Generer(Exemple(), Entete)));
        Assert.Equal(["Synthèse", "Par rubrique"], classeur.Worksheets.Select(f => f.Name));

        var feuille = classeur.Worksheet("Par rubrique");
        var textes = feuille.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Fonds routier (FR)", textes);
        Assert.Contains("Exercice : 2027", textes);
        Assert.Contains("n.s.", textes);                                     // taux sans dénominateur

        var total = feuille.CellsUsed().First(c => c.GetString() == "Total");
        var cellule = total.CellRight();
        Assert.StartsWith("SUM(", cellule.FormulaA1);                        // total par formule
        Assert.Equal("#,##0.00", cellule.Style.NumberFormat.Format);
        Assert.Contains("Exécution conforme.", classeur.Worksheet("Synthèse").CellsUsed().Select(c => c.GetString()));
    }

    [Fact]
    public void Rendu_PDF_et_formats_francais()
    {
        var pdf = RenduPdf.Generer(Exemple() with { Paysage = true }, Entete);
        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        Assert.Equal("1 234,50", RenduPdf.Formater(1234.5m, TypeColonne.Montant).Replace(' ', ' ').Replace(' ', ' '));
        Assert.Equal("54,17 %", RenduPdf.Formater(54.17m, TypeColonne.Taux));
        Assert.Equal("n.s.", RenduPdf.Formater(null, TypeColonne.Taux));
        Assert.Equal("", RenduPdf.Formater(null, TypeColonne.Montant));
    }

    // ═════ Service et générateurs sur base en mémoire ═════════════════════════

    private sealed record Scene(Environnement Env, Ulid Organisme, Utilisateur Utilisateur, Ulid ExerciceId, Ulid CycleId, AuditFactice Audit)
    {
        public AccesParametrage AccesParametrage() => new(Env.UtilisateurCourant, Env.Droits);
        private ControlePerimetre Perimetre() => new(Env.Fabrique(Organisme));

        public RapportService Rapports()
        {
            var fabrique = Env.Fabrique(Organisme);
            var audit = new AuditFactice();
            var axes = new AccesAxes(AccesParametrage(), Perimetre());
            var bp = new AccesBudgetProgramme(AccesParametrage(), Perimetre());
            var generateur = new GenerateurRapports(
                new ConsommationService(fabrique, new AccesConsommation(AccesParametrage(), Perimetre())),
                new SyntheseAxeService(fabrique, axes), new ProjetService(fabrique, axes, audit), new AvancementService(fabrique, axes, audit),
                new ImmobilisationService(fabrique, axes, audit), new SyntheseRecetteService(fabrique, new AccesRecettes(AccesParametrage(), Perimetre())),
                audit, new ProgrammationService(fabrique, bp, audit), new IndicateurService(fabrique, bp, audit), new CadreLogiqueService(fabrique, bp, audit),
                new ProgrammeService(fabrique, bp, audit), new AnalyseService(fabrique, bp, audit), new CycleProgrammationService(fabrique, AccesParametrage(), audit),
                new ExerciceService(fabrique, AccesParametrage(), audit),
                new CompteSurChiffresService(fabrique, new AccesConsommation(AccesParametrage(), Perimetre()), new AccesRecettes(AccesParametrage(), Perimetre())),
                new BesoinEntretienService(fabrique, axes, audit));
            return new RapportService(fabrique, AccesParametrage(), Env.Droits, generateur, Audit);
        }
    }

    /// <summary>
    /// Organisme provisionné ; exercice 2027 ouvert (mois, trimestres, semestres, année) avec une
    /// dotation de fonctionnement engagée et payée ; cycle 2027-2029 avec un programme, une action,
    /// une programmation et un indicateur ; un utilisateur qui lit tout et exporte.
    /// </summary>
    private static async Task<Scene> PreparerAsync()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var utilisateur = await env.CreerUtilisateurAsync("rapports", org);
        TypeDroit[] droits =
        [
            TypeDroit.RapportExporter, TypeDroit.ConsommationLire, TypeDroit.EntretienLire, TypeDroit.InvestissementLire, TypeDroit.AppuisLire,
            TypeDroit.RecettesLire, TypeDroit.BudgetProgrammeLire, TypeDroit.AuditConsultation, TypeDroit.ExecutionLire,
            TypeDroit.DotationSaisir, TypeDroit.EngagementSaisir, TypeDroit.EngagementValider, TypeDroit.PaiementSaisir, TypeDroit.ParametrageGerer, TypeDroit.ParametrageLire
        ];
        env.Droits.Accorder(utilisateur.Id, droits);
        env.UtilisateurCourant.Utilisateur = utilisateur;

        await using var ctx = env.Contexte(org);
        await ProvisionnementOrganisme.CompleterAsync(ctx, org, Ulid.Empty);
        await ctx.SaveChangesAsync();

        var axe = await ctx.Axes.FirstAsync(a => a.Code == "FONCTIONNEMENT");
        var nature = await ctx.NaturesDepense.FirstAsync(n => n.Code == "2");
        var source = await ctx.SourcesFinancement.FirstAsync(s => s.Code == "RI");
        var ligne = new LigneBudgetaire
        {
            SousRubrique = new SousRubrique { Rubrique = new Rubrique { AxeId = axe.Id, Code = "60", Libelle = "Achats" }, Code = "601", Libelle = "Fournitures" },
            Code = "60100", Libelle = "Fournitures", Sens = SensLigne.Depense, NatureDepenseId = nature.Id, SourceFinancementId = source.Id
        };
        var cycle = new CycleProgrammation { AnneeReference = 2026, AnneeBase = 2027, AnneeFin = 2029, Libelle = "2027-2029", Statut = StatutCycle.Valide };
        var exercice = new Exercice { Annee = 2027, DateDebut = new(2027, 1, 1), DateFin = new(2027, 12, 31), Statut = StatutExercice.EnPreparation, CycleProgrammation = cycle };
        for (var m = 1; m <= 12; m++)
            exercice.Periodes.Add(new Periode { TypePeriode = TypePeriode.Mois, Numero = m, DateDebut = new(2027, m, 1), DateFin = new(2027, m, DateTime.DaysInMonth(2027, m)) });
        for (var t = 1; t <= 4; t++)
            exercice.Periodes.Add(new Periode { TypePeriode = TypePeriode.Trimestre, Numero = t, DateDebut = new(2027, 3 * t - 2, 1), DateFin = new DateOnly(2027, 3 * t, 1).AddMonths(1).AddDays(-1) });
        exercice.Periodes.Add(new Periode { TypePeriode = TypePeriode.Semestre, Numero = 1, DateDebut = new(2027, 1, 1), DateFin = new(2027, 6, 30) });
        exercice.Periodes.Add(new Periode { TypePeriode = TypePeriode.Semestre, Numero = 2, DateDebut = new(2027, 7, 1), DateFin = new(2027, 12, 31) });
        exercice.Periodes.Add(new Periode { TypePeriode = TypePeriode.Annee, Numero = 1, DateDebut = new(2027, 1, 1), DateFin = new(2027, 12, 31) });

        var programme = new Programme { Code = "P1", Libelle = "Programme 1", ResponsableId = utilisateur.Id };
        var action = new ActionProgramme { Programme = programme, Code = "A1", Libelle = "Action 1" };
        var activite = new Activite { Action = action, Code = "ACT1", Libelle = "Activité 1" };
        var unite = await ctx.UnitesMesure.FirstAsync();
        var indicateur = new Indicateur { Code = "I1", Libelle = "Taux d'entretien", Niveau = NiveauIndicateur.Programme, Programme = programme, UniteMesureId = unite.Id };
        ctx.AddRange(ligne, cycle, exercice, programme, action, activite, indicateur);
        ctx.ClesRepartition.Add(new CleRepartition { Exercice = exercice, LigneBudgetaire = ligne, Activite = activite, Pourcentage = 100 });
        ctx.ProgrammationsFinancieres.Add(new ProgrammationFinanciere
        {
            CycleProgrammation = cycle, Annee = 2027, Action = action, NatureDepenseId = nature.Id, SourceFinancementId = source.Id, MontantAE = 900_000, MontantCP = 700_000
        });
        ctx.Habilitations.Add(new Habilitation
        {
            UtilisateurId = utilisateur.Id, TypePerimetre = TypePerimetre.OrganismeEntier, NiveauAcces = NiveauAcces.Saisie,
            DateDebut = new DateOnly(2020, 1, 1), Statut = StatutHabilitation.Active, DemandeParId = utilisateur.Id
        });
        await ctx.SaveChangesAsync();

        var s = new Scene(env, org, utilisateur, exercice.Id, cycle.Id, new AuditFactice());
        var acces = new AccesExecution(s.AccesParametrage(), new ControlePerimetre(env.Fabrique(org)));
        var notifications = new NotificationsFactices();
        Assert.True((await new DotationService(env.Fabrique(org), acces, new AuditFactice()).EnregistrerAsync(
            new SaisieDotation { ExerciceId = exercice.Id, LigneBudgetaireId = ligne.Id, MontantInitial = 1_000_000 })).Ok);
        Assert.True((await new ExerciceService(env.Fabrique(org), s.AccesParametrage(), new AuditFactice()).OuvrirAsync(exercice.Id)).Ok);
        var engagements = new EngagementService(env.Fabrique(org), acces, new AuditFactice(),
            new AlerteService(env.Fabrique(org), env.Droits, notifications, NullLogger<AlerteService>.Instance), env.Droits, notifications);
        Ulid dotationId;
        await using (var c2 = env.Contexte(org)) dotationId = await c2.Dotations.Select(d => d.Id).SingleAsync();
        var (_, _, engagementId) = await engagements.EnregistrerAsync(new SaisieEngagement { DotationId = dotationId, Date = new(2027, 2, 1), Objet = "Achats", Montant = 400_000 });
        Assert.True((await engagements.SoumettreAsync(engagementId, null)).Ok);
        Assert.True((await engagements.ValiderAsync(engagementId)).Ok);
        Assert.True((await new PaiementService(env.Fabrique(org), acces, new AuditFactice()).PayerAsync(
            new SaisiePaiement { EngagementId = engagementId, NumeroPiece = "P1", Date = new(2027, 3, 1), Montant = 250_000 })).Ok);
        return s;
    }

    private static async Task<Ulid> PeriodeAsync(Scene s, TypePeriode type, int numero)
    {
        await using var ctx = s.Env.Contexte(s.Organisme);
        return await ctx.Periodes.Where(p => p.ExerciceId == s.ExerciceId && p.TypePeriode == type && p.Numero == numero).Select(p => p.Id).FirstAsync();
    }

    [Fact]
    public async Task Catalogue_filtre_par_droits_et_parametres_controles()
    {
        var s = await PreparerAsync();
        var service = s.Rapports();
        Assert.Equal(CatalogueRapports.Tous.Count, (await service.ListerDisponiblesAsync()).Count);

        // Sans la lecture de l'audit ni du budget-programme : ces états ne sont pas proposés.
        var lecteur = await s.Env.CreerUtilisateurAsync("lecteur", s.Organisme);
        s.Env.Droits.Accorder(lecteur.Id, TypeDroit.RapportExporter, TypeDroit.ConsommationLire);
        s.Env.UtilisateurCourant.Utilisateur = lecteur;
        var disponibles = (await service.ListerDisponiblesAsync()).Select(r => r.Code).ToList();
        Assert.Contains("SYNTHESE_GLOBALE", disponibles);
        Assert.DoesNotContain("AUDIT", disponibles);
        Assert.DoesNotContain("DPPD", disponibles);
        Assert.Contains("accès aux données", (await service.GenererAsync("AUDIT", new ParametresRapport(), FormatRapport.Pdf)).Erreur);

        // Sans RapportExporter : rien.
        var autre = await s.Env.CreerUtilisateurAsync("autre", s.Organisme);
        s.Env.Droits.Accorder(autre.Id, TypeDroit.ConsommationLire);
        s.Env.UtilisateurCourant.Utilisateur = autre;
        Assert.Empty(await service.ListerDisponiblesAsync());
        Assert.False((await service.GenererAsync("SYNTHESE_GLOBALE", new ParametresRapport { ExerciceId = s.ExerciceId }, FormatRapport.Excel)).Ok);

        // Paramètres exigés et cohérents.
        s.Env.UtilisateurCourant.Utilisateur = s.Utilisateur;
        Assert.Contains("exercice", (await service.GenererAsync("SYNTHESE_GLOBALE", new ParametresRapport(), FormatRapport.Excel)).Erreur);
        Assert.Contains("axe", (await service.GenererAsync("SITUATION_AXE", new ParametresRapport { ExerciceId = s.ExerciceId }, FormatRapport.Excel)).Erreur);
        var mois = await PeriodeAsync(s, TypePeriode.Mois, 3);
        Assert.Contains("type attendu", (await service.GenererAsync("RAPPORT_TRIMESTRIEL", new ParametresRapport { ExerciceId = s.ExerciceId, PeriodeId = mois }, FormatRapport.Pdf)).Erreur);
        Assert.Contains("fin précède", (await service.GenererAsync("AUDIT", new ParametresRapport { Du = new(2027, 5, 1), Au = new(2027, 4, 1) }, FormatRapport.Pdf)).Erreur);
        Assert.Contains("cycle", (await service.GenererAsync("DPPD", new ParametresRapport(), FormatRapport.Pdf)).Erreur);
    }

    [Fact]
    public async Task Chaque_etat_se_produit_en_Excel_et_en_PDF_et_est_journalise()
    {
        var s = await PreparerAsync();
        var service = s.Rapports();
        Ulid axeFonct;
        await using (var ctx = s.Env.Contexte(s.Organisme)) axeFonct = await ctx.Axes.Where(a => a.Code == "FONCTIONNEMENT").Select(a => a.Id).FirstAsync();
        var trimestre = await PeriodeAsync(s, TypePeriode.Trimestre, 1);
        var semestre = await PeriodeAsync(s, TypePeriode.Semestre, 1);

        foreach (var d in CatalogueRapports.Tous)
        {
            var p = new ParametresRapport
            {
                ExerciceId = s.ExerciceId, CycleId = s.CycleId, AxeId = d.Code == "SITUATION_AXE" ? axeFonct : null,
                PeriodeId = d.Parametres.HasFlag(ParametresDemandes.Trimestre) ? trimestre : d.Parametres.HasFlag(ParametresDemandes.Semestre) ? semestre : null,
                Du = new(2027, 1, 1), Au = new(2027, 12, 31)
            };
            foreach (var format in new[] { FormatRapport.Excel, FormatRapport.Pdf })
            {
                var (ok, erreur, fichier) = await service.GenererAsync(d.Code, p, format);
                Assert.True(ok, $"{d.Code} ({format}) : {erreur}");
                Assert.True(fichier!.Contenu.Length > 500, d.Code);
                Assert.EndsWith(format == FormatRapport.Excel ? ".xlsx" : ".pdf", fichier.NomFichier);
            }
        }
        Assert.Equal(CatalogueRapports.Tous.Count * 2, s.Audit.Actions.Count(a => a == "EXPORT"));
    }

    [Fact]
    public async Task Synthese_globale_reprend_les_montants_et_les_filtres()
    {
        var s = await PreparerAsync();
        var (ok, erreur, fichier) = await s.Rapports().GenererAsync("SYNTHESE_GLOBALE", new ParametresRapport { ExerciceId = s.ExerciceId }, FormatRapport.Excel);
        Assert.True(ok, erreur);
        using var classeur = new XLWorkbook(new MemoryStream(fichier!.Contenu));
        var feuille = classeur.Worksheet("Situation consolidée par axe");
        Assert.Contains("Exercice : 2027", feuille.CellsUsed().Select(c => c.GetString()));
        var ligne = feuille.CellsUsed().First(c => c.GetString() == "Fonctionnement").WorksheetRow();
        Assert.Equal(1_000_000m, ligne.Cell(4).GetValue<decimal>());     // dotation totale
        Assert.Equal(400_000m, ligne.Cell(5).GetValue<decimal>());       // engagé
        Assert.Equal(250_000m, ligne.Cell(7).GetValue<decimal>());       // payé

        var dppd = await s.Rapports().GenererAsync("DPPD", new ParametresRapport { CycleId = s.CycleId }, FormatRapport.Excel);
        using var classeurDppd = new XLWorkbook(new MemoryStream(dppd.Fichier!.Contenu));
        var ae = classeurDppd.Worksheets.First(f => f.Name.StartsWith("Autorisations"));
        var programme = ae.CellsUsed().First(c => c.GetString() == "Programme P1").WorksheetRow();
        Assert.Equal(900_000m, programme.Cell(3).GetValue<decimal>());   // 2027 (après 2026 de référence)
        Assert.Equal(900_000m, programme.Cell(6).GetValue<decimal>());   // total triennal
    }
}
