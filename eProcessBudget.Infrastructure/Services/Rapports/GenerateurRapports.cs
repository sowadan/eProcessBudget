using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Axes;
using eProcessBudget.Infrastructure.Services.BudgetProgramme;
using eProcessBudget.Infrastructure.Services.Consommation;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Recettes;
using static eProcessBudget.Infrastructure.Services.Rapports.Lignes;

namespace eProcessBudget.Infrastructure.Services.Rapports;

/// <summary>
/// Construit le modèle de chaque état du catalogue à partir des services des modules : un
/// état ne recalcule rien, il présente ce que montrent les écrans (mêmes formules, mêmes
/// droits, même périmètre de lecture).
/// </summary>
public sealed class GenerateurRapports
{
    private const int TaillePage = 200;
    private const int AuditMax = 5000;

    private readonly IConsommationService _consommation;
    private readonly ISyntheseAxeService _axes;
    private readonly IProjetService _projets;
    private readonly IAvancementService _avancement;
    private readonly IImmobilisationService _immobilisations;
    private readonly ISyntheseRecetteService _recettes;
    private readonly IAuditService _audit;
    private readonly IProgrammationService _programmation;
    private readonly IIndicateurService _indicateurs;
    private readonly ICadreLogiqueService _cadreLogique;
    private readonly IProgrammeService _programmes;
    private readonly IAnalyseService _analyses;
    private readonly ICycleProgrammationService _cycles;
    private readonly IExerciceService _exercices;
    private readonly ICompteSurChiffresService _chiffres;
    private readonly IBesoinEntretienService _besoins;

    public GenerateurRapports(IConsommationService consommation, ISyntheseAxeService axes, IProjetService projets, IAvancementService avancement,
        IImmobilisationService immobilisations, ISyntheseRecetteService recettes, IAuditService audit, IProgrammationService programmation,
        IIndicateurService indicateurs, ICadreLogiqueService cadreLogique, IProgrammeService programmes, IAnalyseService analyses,
        ICycleProgrammationService cycles, IExerciceService exercices, ICompteSurChiffresService chiffres, IBesoinEntretienService besoins)
    {
        _chiffres = chiffres;
        _besoins = besoins;
        _consommation = consommation;
        _axes = axes;
        _projets = projets;
        _avancement = avancement;
        _immobilisations = immobilisations;
        _recettes = recettes;
        _audit = audit;
        _programmation = programmation;
        _indicateurs = indicateurs;
        _cadreLogique = cadreLogique;
        _programmes = programmes;
        _analyses = analyses;
        _cycles = cycles;
        _exercices = exercices;
    }

    public Task<List<SectionRapport>> GenererAsync(DescripteurRapport d, ParametresRapport p) => d.Code switch
    {
        "SITUATION_AXE" => SituationAxeAsync(p),
        "SYNTHESE_GLOBALE" => SyntheseGlobaleAsync(p),
        "CONSOMMATION_RUBRIQUE_COMPTE" => ConsommationRubriqueCompteAsync(p),
        "AVANCEMENT_TRAVAUX" => AvancementTravauxAsync(p),
        "INVESTISSEMENTS_SITES" => InvestissementsSitesAsync(p),
        "APPUIS" => AppuisAsync(p),
        "BILAN" => BilanAsync(p),
        "COMPTE_CHIFFRES_RECETTES" => CompteChiffresRecettesAsync(p),
        "COMPTE_CHIFFRES_DEPENSES" => CompteChiffresDepensesAsync(p),
        "EQUILIBRE_TRESORERIE" => EquilibreTresorerieAsync(p),
        "ETAT_PRESENTATION_BUDGET" => EtatPresentationAsync(p),
        "PROJECTION" => ProjectionAsync(p),
        "AUDIT" => AuditAsync(p),
        "DPPD" => DppdAsync(p),
        "RESSOURCES" => RessourcesAsync(p),
        "INDICATEURS" => IndicateursAsync(p),
        "CADRE_LOGIQUE" => CadreLogiqueAsync(p),
        "EMPLOIS" => EmploisAsync(p),
        "EXECUTION_TROIS_EXERCICES" => ExecutionTroisExercicesAsync(p),
        "RAPPORT_TRIMESTRIEL" => RapportTrimestrielAsync(p),
        "RAPPORT_SEMESTRIEL" => RapportSemestrielAsync(p),
        "RAPPORT_ANNUEL_PERFORMANCE" => RapportAnnuelAsync(p),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d.Code, "État inconnu."),
    };

    // ═════ Outils communs ═════════════════════════════════════════════════════

    private static FiltreConsommation Filtre(ParametresRapport p)
        => new() { ExerciceId = p.ExerciceId!.Value, AxeId = p.AxeId, PeriodeId = p.PeriodeId };

    private static string M(decimal? montant) => RenduPdf.Formater(montant, TypeColonne.Montant);
    private static string T(decimal? taux) => RenduPdf.Formater(taux, TypeColonne.Taux);

    /// <summary>Colonnes de situation d'une imputation (cycle commun).</summary>
    private static List<ColonneRapport> ColonnesSituation(string premiere) =>
    [
        Texte(premiere, 3), Montant("Prévision initiale"), Montant("Prévision ajustée"), Montant("Dotation totale"),
        Montant("Engagé (AE / AD)"), Montant("Payé"), Montant("Reste à payer"), Taux("Taux d'engagement"), Taux("Taux de consommation"),
    ];

    private static object?[] CellulesSituation(string libelle, MontantsDotation m) =>
    [
        libelle, m.MontantInitial, m.PrevisionAjustee, m.DotationTotale, m.AETotal, m.PaiementsTotal, m.ResteAPayerYCReports,
        m.TauxEngagementYCReports, m.TauxConsommationYCReports,
    ];

    private static TableauRapport TableauRegroupement(string premiere, IReadOnlyList<LigneRegroupement> lignes) => new(
        ColonnesSituation(premiere),
        lignes.Select(l => L(CellulesSituation(l.Libelle, l.Montants))).ToList(),
        L(CellulesSituation("Total", MontantsDotation.Somme(lignes.Select(l => l.Montants)))));

    private static IReadOnlyList<(string, string)> ChiffresSituation(MontantsDotation m) =>
    [
        ("Dotation totale", M(m.DotationTotale)), ("Engagé (AE / AD)", M(m.AETotal)), ("Payé", M(m.PaiementsTotal)),
        ("Reste à payer", M(m.ResteAPayerYCReports)), ("Taux d'engagement", T(m.TauxEngagementYCReports)), ("Taux de consommation", T(m.TauxConsommationYCReports)),
    ];

    private async Task<LigneCycle?> CycleAsync(ParametresRapport p) => (await _cycles.ListerAsync()).FirstOrDefault(c => c.Id == p.CycleId);

    private static int[] AnneesCycle(LigneCycle c) => Enumerable.Range(c.AnneeReference, c.AnneeFin - c.AnneeReference + 1).ToArray();

    // ═════ Rapports de gestion budgétaire (§ 3.12.1) ═══════════════════════════

    private async Task<List<SectionRapport>> SituationAxeAsync(ParametresRapport p)
    {
        var lignes = await _consommation.SituationDetailleeAsync(Filtre(p));
        var tableau = new List<LigneRapport>();
        foreach (var rubrique in lignes.GroupBy(l => l.Rubrique))
        {
            tableau.Add(Fort(CellulesSituation(rubrique.Key, MontantsDotation.Somme(rubrique.Select(l => l.Montants)))));
            tableau.AddRange(rubrique.Select(l => Detail(CellulesSituation(
                $"{l.LigneCode} — {l.LigneLibelle}{(l.Imputation.Length > 0 ? " · " + l.Imputation : "")}", l.Montants))));
        }
        var total = MontantsDotation.Somme(lignes.Select(l => l.Montants));
        return
        [
            new("Chiffres clés", Chiffres: ChiffresSituation(total)),
            new("Situation par rubrique et par ligne", new TableauRapport(ColonnesSituation("Rubrique / ligne · imputation"), tableau, L(CellulesSituation("Total", total)))),
        ];
    }

    private async Task<List<SectionRapport>> SyntheseGlobaleAsync(ParametresRapport p)
    {
        var c = await _consommation.ConsoliderAsync(Filtre(p));
        if (c is null) return [];
        var colonnes = new List<ColonneRapport>
        {
            Texte("Axe"), Montant("Prévision initiale"), Montant("Prévision ajustée"), Montant("Dotation totale"), Montant("Engagé"),
            Montant("Factures reçues"), Montant("Payé"), Montant("Rejets / pénalités"), Montant("Reste à payer"), Montant("Reliquat"),
            Taux("Taux d'engagement"), Taux("Taux de consommation"),
        };
        object?[] Cellules(string libelle, MontantsDotation m, decimal factures) =>
        [
            libelle, m.MontantInitial, m.PrevisionAjustee, m.DotationTotale, m.AETotal, factures, m.PaiementsTotal, m.RejetsPenalites,
            m.ResteAPayerYCReports, m.Ecart, m.TauxEngagementYCReports, m.TauxConsommationYCReports,
        ];
        var sections = new List<SectionRapport>
        {
            new("Chiffres clés", Chiffres: [.. ChiffresSituation(c.Total), ("Paiements projetés en fin d'exercice", M(c.PaiementsProjetes)), ("Taux projeté", T(c.TauxProjete))]),
            new("Situation consolidée par axe", new TableauRapport(colonnes, c.Axes.Select(a => L(Cellules(a.Axe, a.Montants, a.Factures))).ToList(),
                L(Cellules("Total", c.Total, c.Factures)))),
        };
        if (c.RecettesVisibles)
            sections.Add(new("Recettes en regard", Chiffres:
            [
                ("Prévision retenue", M(c.Recettes.PrevisionRetenue)), ("Recouvré", M(c.Recettes.Encaisse)),
                ("Reste à recouvrer", M(c.Recettes.Ecart)), ("Taux de recouvrement", T(c.Recettes.TauxRecouvrement)),
            ]));
        return sections;
    }

    private async Task<List<SectionRapport>> ConsommationRubriqueCompteAsync(ParametresRapport p)
    {
        var f = Filtre(p);
        return
        [
            new("Par rubrique", TableauRegroupement("Rubrique", await _consommation.RegrouperAsync(f, Regroupement.Rubrique))),
            new("Par compte comptable", TableauRegroupement("Compte (principal de la ligne)", await _consommation.RegrouperAsync(f, Regroupement.Compte))),
        ];
    }

    private async Task<List<SectionRapport>> AvancementTravauxAsync(ParametresRapport p)
    {
        var projets = new List<LigneProjet>();
        foreach (var axe in new[] { AxesDepense.Entretien, AxesDepense.Investissement })
        {
            for (var page = 1; ; page++)
            {
                var (items, total) = await _projets.ListerAsync(axe, new FiltreProjets { ExerciceId = p.ExerciceId }, page, TaillePage);
                // Investissements : les seuls projets de réhabilitation (opérations de travaux, C.2.4).
                projets.AddRange(axe == AxesDepense.Entretien ? items : items.Where(i => i.TypeTravaux is not null));
                if (page * TaillePage >= total || items.Count == 0) break;
            }
        }
        var synthese = await _avancement.SyntheseAsync(p.ExerciceId!.Value);
        var indicateurs = await _besoins.IndicateursAsync(p.ExerciceId!.Value);
        return
        [
            new("Indicateurs de l'entretien routier", Chiffres: indicateurs is not { } i ? [] :
            [
                ("Prévisions affectées", M(i.PrevisionsAffectees)), ("AD", M(i.Financement.AETotal)), ("Crédits de paiement", M(i.Financement.PaiementsTotal)),
                ("Besoins évalués", M(i.BesoinsEvalues)), ("Taux de conformité (AD / prévisions)", T(i.TauxConformite)),
                ("Couverture des besoins (financement / besoins)", T(i.TauxCouverture)), ("Exécution physique (CP / AD)", T(i.TauxExecutionPhysique)),
            ]),
            new("Opérations de travaux (entretien et réhabilitation)", new TableauRapport(
            [
                Texte("Opération", 3), Texte("Type de travaux", 2), Texte("Statut", 1), Nombre("Km prévus"), Nombre("Km entretenus"), Taux("Avancement km"),
                Montant("Dotation totale"), Montant("Engagé"), Montant("Payé"), Taux("Taux de consommation"),
            ],
            projets.Select(o => L($"{o.Code} — {o.Intitule}", o.TypeTravaux, LibellesAxes.StatutProjet[o.Statut], o.KmPrevus, o.KmRealises, o.TauxKm,
                o.Montants.DotationTotale, o.Montants.AETotal, o.Montants.PaiementsTotal, o.Montants.TauxConsommationYCReports)).ToList(),
            L("Total", null, null, projets.Sum(o => o.KmPrevus ?? 0), projets.Sum(o => o.KmRealises),
                CalculsAxes.TauxAvancementKm(projets.Sum(o => o.KmRealises), projets.Sum(o => o.KmPrevus ?? 0)),
                projets.Sum(o => o.Montants.DotationTotale), projets.Sum(o => o.Montants.AETotal), projets.Sum(o => o.Montants.PaiementsTotal),
                CalculsExecution.Taux(projets.Sum(o => o.Montants.PaiementsTotal), projets.Sum(o => o.Montants.DotationTotale))))),
            new("Kilométrage par type de travaux", new TableauRapport(
                [Texte("Type de travaux", 3), Nombre("Km prévus"), Nombre("Km entretenus (exercice)"), Nombre("Km entretenus (cumul)"), Taux("Avancement")],
                synthese.Select(k => L(k.TypeTravaux, k.KmPrevus, k.KmExercice, k.KmCumules, k.Taux)).ToList(),
                L("Total", synthese.Sum(k => k.KmPrevus), synthese.Sum(k => k.KmExercice), synthese.Sum(k => k.KmCumules),
                    CalculsAxes.TauxAvancementKm(synthese.Sum(k => k.KmCumules), synthese.Sum(k => k.KmPrevus))))),
        ];
    }

    private async Task<List<SectionRapport>> InvestissementsSitesAsync(ParametresRapport p)
    {
        var sites = await _axes.RegrouperAsync(AxesDepense.Investissement, p.ExerciceId!.Value, Regroupement.Site);
        var inventaire = await _immobilisations.ParSiteAsync();
        return
        [
            new("Situation des investissements par site", TableauRegroupement("Site", sites)),
            new("Inventaire des immobilisations par site", new TableauRapport(
                [Texte("Site", 3), Texte("Type", 1.5f), Nombre("Biens"), Nombre("Hors service"), Montant("Valeur d'acquisition")],
                inventaire.Select(s => L(s.Site, LibellesParametrage.De(LibellesParametrage.TypeSite, s.TypeSite), s.NbBiens, s.NbHorsService, s.Valeur)).ToList(),
                L("Total", null, inventaire.Sum(s => s.NbBiens), inventaire.Sum(s => s.NbHorsService), inventaire.Sum(s => s.Valeur)))),
        ];
    }

    private async Task<List<SectionRapport>> AppuisAsync(ParametresRapport p)
    {
        var entites = await _axes.AppuisParEntiteAsync(p.ExerciceId!.Value);
        var attente = await _axes.AppuisEnAttenteAsync(p.ExerciceId.Value);
        var maintenant = DateTime.UtcNow;
        return
        [
            new("Appuis par entité", new TableauRapport(
                [Texte("Entité", 3), Texte("Rattachement", 2), Texte("Catégorie", 1.5f), Texte("Sites", 2), Montant("Prévu"), Montant("Engagé"), Montant("Versé"), Montant("Solde")],
                entites.Select(e => L(e.Entite, e.Parent, e.Categorie, e.Sites, e.Montants.DotationTotale, e.Montants.AETotal, e.Montants.PaiementsTotal, e.Solde)).ToList(),
                L("Total", null, null, null, entites.Sum(e => e.Montants.DotationTotale), entites.Sum(e => e.Montants.AETotal),
                    entites.Sum(e => e.Montants.PaiementsTotal), entites.Sum(e => e.Solde)))),
            new("Appuis en attente de versement", new TableauRapport(
                [Texte("Engagement", 3), Texte("Entité", 2), Texte("Validé le", 1), Montant("Montant"), Montant("Versé"), Montant("Reste à verser"), Nombre("Jours d'attente")],
                attente.Select(a => L($"{a.Numero} — {a.Objet}", a.Entite, a.DateValidation?.ToLocalTime().ToString("dd/MM/yyyy"), a.Montant, a.Verse, a.Reste,
                    a.JoursAttente(maintenant))).ToList(),
                L("Total", null, null, attente.Sum(a => a.Montant), attente.Sum(a => a.Verse), attente.Sum(a => a.Reste), null))),
        ];
    }

    // ── Compte sur chiffres et états de fin d'exercice (réponses C.2.7 et C.2.9) ──

    private static readonly List<ColonneRapport> ColonnesRecettes =
    [
        Texte("Libellé des lignes de recettes", 3), Montant("Prévisions ajustées"), Montant("Factures émises"), Taux("% exéc."),
        Montant("Factures recouvrées"), Taux("% rec."), Montant("Reste à recouvrer"), Taux("% RAR"),
    ];

    private static object?[] CellulesRecette(string libelle, ChiffresRecette c) =>
        [libelle, c.Prevision, c.Emis, c.TauxExecution, c.Recouvre, c.TauxRecouvrement, c.ResteARecouvrer, c.TauxResteARecouvrer];

    private static readonly List<ColonneRapport> ColonnesDepenses =
    [
        Texte("Libellé des chapitres de dépenses", 3), Montant("Prévisions ajustées"), Montant("Prise en charge (factures reçues)"), Taux("% exéc."),
        Montant("Rejets ou pénalités"), Montant("Paiements"), Taux("% pmt"), Montant("Reste à payer"), Taux("% RAP"),
    ];

    private static object?[] CellulesDepense(string libelle, ChiffresDepense c) =>
        [libelle, c.Prevision, c.PriseEnCharge, c.TauxExecution, c.Rejets, c.Paiements, c.TauxPaiement, c.ResteAPayer, c.TauxResteAPayer];

    private static string Libelle(string code, string libelle) => code.Length == 0 ? libelle : $"{code} : {libelle}";

    private static readonly (Section Section, string Libelle)[] Sections =
        [(Section.Fonctionnement, "fonctionnement"), (Section.Investissement, "investissement"), (Section.HorsSections, "hors sections")];

    private async Task<List<SectionRapport>> CompteChiffresRecettesAsync(ParametresRapport p)
    {
        var c = await _chiffres.RecettesAsync(p.ExerciceId!.Value);
        if (c is null) return [];
        var lignes = new List<LigneRapport>();
        foreach (var (section, libelle) in Sections)
        {
            var dela = c.Lignes.Where(l => l.Section == section).ToList();
            if (dela.Count == 0 && section == Section.HorsSections) continue;
            lignes.AddRange(dela.Select(l => L(CellulesRecette(Libelle(l.Code, l.Libelle), l.Chiffres))));
            lignes.Add(Fort(CellulesRecette($"Total recettes de {libelle}", c.Total(section))));
        }
        lignes.Add(Fort(CellulesRecette("Total recettes budgétaires", c.TotalBudgetaire)));
        lignes.AddRange(c.Reports.Select(l => L(CellulesRecette(Libelle(l.Code, l.Libelle), l.Chiffres))));
        lignes.Add(Fort(CellulesRecette("Total reports de ressources", c.TotalReports)));
        var g = c.TotalGeneral;
        return
        [
            new("Chiffres clés", Chiffres:
            [
                ("Prévisions ajustées", M(g.Prevision)), ("Factures émises", M(g.Emis)), ("% d'exécution", T(g.TauxExecution)),
                ("Recouvré", M(g.Recouvre)), ("% de recouvrement", T(g.TauxRecouvrement)), ("Reste à recouvrer", M(g.ResteARecouvrer)),
            ]),
            new($"Compte sur chiffres : recettes au 31/12/{c.Annee}", new TableauRapport(ColonnesRecettes, lignes,
                Fort(CellulesRecette("Total général du budget des ressources", g)))),
        ];
    }

    private async Task<List<SectionRapport>> CompteChiffresDepensesAsync(ParametresRapport p)
    {
        var c = await _chiffres.DepensesAsync(p.ExerciceId!.Value);
        if (c is null) return [];
        var lignes = new List<LigneRapport>();
        foreach (var (section, libelle) in Sections)
        {
            var dela = c.Chapitres.Where(l => l.Section == section).ToList();
            if (dela.Count == 0 && section == Section.HorsSections) continue;
            lignes.AddRange(dela.Select(l => L(CellulesDepense(Libelle(l.Code, l.Libelle), l.Chiffres))));
            lignes.Add(Fort(CellulesDepense($"Total dépenses de {libelle}", c.Total(section))));
        }
        lignes.Add(Fort(CellulesDepense("Total dépenses budgétaires", c.TotalBudgetaire)));
        lignes.Add(L(CellulesDepense("Total reports d'engagements antérieurs", c.ReportsAnterieurs)));
        var g = c.TotalGeneral;
        return
        [
            new("Chiffres clés", Chiffres:
            [
                ("Prévisions ajustées", M(g.Prevision)), ("Prise en charge", M(g.PriseEnCharge)), ("% d'exécution", T(g.TauxExecution)),
                ("Paiements", M(g.Paiements)), ("% de paiement", T(g.TauxPaiement)), ("Reste à payer", M(g.ResteAPayer)),
            ]),
            new($"Compte sur chiffres : dépenses au 31/12/{c.Annee}", new TableauRapport(ColonnesDepenses, lignes,
                Fort(CellulesDepense("Total général du budget des dépenses", g)))),
            new("Pour mémoire", new TableauRapport(ColonnesDepenses,
                [Detail(CellulesDepense("Dont travaux d'entretien routier (entretien et réhabilitation)", c.DontEntretien))])),
        ];
    }

    private async Task<List<SectionRapport>> EquilibreTresorerieAsync(ParametresRapport p)
    {
        var exercices = await _exercices.ListerAsync();
        var exercice = exercices.FirstOrDefault(e => e.Id == p.ExerciceId);
        if (exercice is null) return [];
        var precedent = exercices.FirstOrDefault(e => e.Annee == exercice.Annee - 1);
        var n = await _chiffres.EquilibreAsync(exercice.Id);
        if (n is null) return [];
        var n1 = precedent is null ? null : await _chiffres.EquilibreAsync(precedent.Id);

        LigneRapport Ligne(string libelle, Func<EquilibreTresorerie, decimal> valeur, bool fort = false)
        {
            object?[] cellules = n1 is null ? [libelle, valeur(n)] : [libelle, valeur(n), valeur(n1)];
            return fort ? Fort(cellules) : L(cellules);
        }
        List<ColonneRapport> colonnes = n1 is null
            ? [Texte("Désignations", 4), Montant($"Montant {exercice.Annee}")]
            : [Texte("Désignations", 4), Montant($"Montant {exercice.Annee}"), Montant($"Montant {precedent!.Annee}")];
        return
        [
            new("Équilibre de trésorerie", new TableauRapport(colonnes,
            [
                Ligne("Total recettes recouvrées (A)", e => e.RecettesRecouvrees),
                Ligne("Total des engagements payés (B)", e => e.EngagementsPayes),
                Ligne("Total de la trésorerie sans déduction des encours (C) = (A) − (B)", e => e.TresorerieSansEncours, true),
                Ligne("Total encours des engagements antérieurs (D)", e => e.EncoursAnterieurs),
                Ligne("Total de la trésorerie disponible en début d'année (E) = (C) − (D)", e => e.TresorerieDisponible, true),
                Ligne("Reports de recettes : taxes affectées non recouvrées (F)", e => e.TaxesNonRecouvrees),
                Ligne("Reports de recettes propres non recouvrées (G)", e => e.RecettesPropresNonRecouvrees),
                Ligne("Total des reports de ressources (H) = (E) + (F) + (G)", e => e.ReportsRessources, true),
                Ligne("Total recettes collectées − total des dépenses engagées (I)", e => e.SoldeEmisPrisEnCharge, true),
                Ligne("Équilibre entre les grandeurs (H) et (I)", e => e.Ecart, true),
            ])),
            new("Lecture", Texte: "Le report de l'exercice suivant est la part des AD qui n'ont pas reçu leur crédit de paiement (D) ; "
                + "H = I : les reports de ressources égalent les recettes émises moins les dépenses prises en charge."),
        ];
    }

    private async Task<List<SectionRapport>> EtatPresentationAsync(ParametresRapport p)
    {
        var e = await _chiffres.PresentationAsync(p.ExerciceId!.Value);
        if (e is null) return [];
        List<ColonneRapport> colonnes =
        [
            Texte("Ligne / chapitre", 3), Montant($"Prévision {e.AnneePrecedente}"), Montant($"Réalisé {e.AnneePrecedente}"), Montant($"Prévision {e.Annee}"),
            Montant("Variation"), Taux("Variation %"), Taux($"Part {e.Annee}"), Texte("Commentaire", 3),
        ];
        TableauRapport Tableau(List<LignePresentation> lignes)
        {
            var totalN = lignes.Sum(l => l.PrevisionN);
            var totalN1 = lignes.Sum(l => l.PrevisionN1);
            return new TableauRapport(colonnes,
                lignes.Select(l => new LigneRapport([Libelle(l.Code, l.Libelle), l.PrevisionN1, l.RealiseN1, l.PrevisionN, l.Variation, l.TauxVariation,
                    CalculsExecution.Taux(l.PrevisionN, totalN),
                    l.Commentaire ?? (l.Significative ? "Variation significative : commentaire attendu." : null)], Forte: l.Significative)).ToList(),
                L("Total", totalN1, lignes.Sum(l => l.RealiseN1), totalN, totalN - totalN1, CalculsPresentation.TauxVariation(totalN, totalN1), totalN == 0 ? null : 100m, null));
        }
        var sections = new List<SectionRapport>
        {
            new("Lecture", Texte: $"Prévisions de {e.Annee} face à celles de {e.AnneePrecedente} et à leur réalisation (recouvré, payé). "
                + $"Variations significatives (au moins {CalculsPresentation.SeuilVariationSignificative:0} %) en gras : leur commentaire se saisit "
                + "comme analyse « commentaire de rubrique » du budget-programme."),
        };
        if (e.Recettes.Count > 0) sections.Add(new("Recettes", Tableau(e.Recettes)));
        sections.Add(new("Dépenses par chapitre", Tableau(e.Depenses)));
        return sections;
    }

    private async Task<List<SectionRapport>> BilanAsync(ParametresRapport p)
    {
        var b = await _recettes.BilanAsync(p.ExerciceId!.Value);
        if (b is null) return [];
        TableauRapport Postes(string premiere, List<PosteBilan> postes, string realise) => new(
            [Texte(premiere, 3), Montant("Prévu"), Montant(realise), Taux("Taux de réalisation")],
            postes.Select(x => L(x.Libelle, x.Prevu, x.Realise, x.Taux)).ToList(),
            L("Total", postes.Sum(x => x.Prevu), postes.Sum(x => x.Realise), CalculsExecution.Taux(postes.Sum(x => x.Realise), postes.Sum(x => x.Prevu))));
        return
        [
            new("Équilibre", Chiffres:
            [
                ("Ressources prévues", M(b.RessourcesPrevues)), ("Ressources réalisées", M(b.RessourcesRealisees)),
                ("Dépenses prévues (dotations)", M(b.DepensesPrevues)), ("Dépenses réalisées (payé)", M(b.DepensesRealisees)),
                ("Équilibre prévisionnel", M(b.EquilibrePrevisionnel)), ("Bilan réalisé", M(b.Bilan)),
                ("Ressources positionnées", M(b.Positionne)), ("Ressources acquises non positionnées", M(b.NonPositionne)),
            ]),
            new("Ressources", Postes("Catégorie", b.Ressources, "Réalisé")),
            new("Dépenses", Postes("Axe", b.Depenses, "Payé")),
        ];
    }

    private async Task<List<SectionRapport>> ProjectionAsync(ParametresRapport p)
    {
        var f = Filtre(p);
        var c = await _consommation.ConsoliderAsync(f);
        if (c is null) return [];
        var derapages = await _consommation.DerapagesAsync(f);
        return
        [
            new("Hypothèse", Texte: $"Extrapolation linéaire : paiements à la date de situation divisés par la part écoulée de l'exercice ({c.PartEcoulee * 100:N1} %)."),
            new("Projection par axe", new TableauRapport(
                [Texte("Axe"), Montant("Dotation totale"), Montant("Engagé"), Montant("Payé à date"), Montant("Paiements projetés"), Taux("Taux projeté"), Texte("Dérapage", 1)],
                c.Axes.Select(a => L(a.Axe, a.Montants.DotationTotale, a.Montants.AETotal, a.Montants.PaiementsTotal, a.PaiementsProjetes, a.TauxProjete, a.Derape ? "Oui" : "")).ToList(),
                L("Total", c.Total.DotationTotale, c.Total.AETotal, c.Total.PaiementsTotal, c.PaiementsProjetes, c.TauxProjete, null))),
            new("Dotations qui dérapent", new TableauRapport(
                [Texte("Ligne · imputation", 3), Montant("Dotation totale"), Montant("Engagé"), Montant("Payé"), Montant("Paiements projetés"), Taux("Taux projeté"), Texte("Motif", 2)],
                derapages.Select(d => L($"{d.Dotation.LigneCode} — {d.Dotation.LigneLibelle}{(d.Dotation.Imputation.Length > 0 ? " · " + d.Dotation.Imputation : "")}",
                    d.Dotation.Montants.DotationTotale, d.Dotation.Montants.AETotal, d.Dotation.Montants.PaiementsTotal, d.PaiementsProjetes, d.TauxProjete, d.Motif)).ToList())),
        ];
    }

    private async Task<List<SectionRapport>> AuditAsync(ParametresRapport p)
    {
        var lignes = new List<LigneAudit>();
        // Jours saisis en heure locale, journal horodaté en UTC (même conversion que JournalAuditService).
        DateTime? du = p.Du?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime(),
                  au = p.Au?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime().AddMilliseconds(-1);
        for (var page = 1; lignes.Count < AuditMax; page++)
        {
            var (items, total) = await _audit.RechercherAsync(du: du, au: au, page: page, taille: TaillePage);
            lignes.AddRange(items);
            if (lignes.Count >= total || items.Count == 0) break;
        }
        return
        [
            new($"Opérations journalisées ({lignes.Count}{(lignes.Count >= AuditMax ? " premières" : "")})", new TableauRapport(
                [Texte("Date", 1.4f), Texte("Utilisateur", 1.2f), Texte("Module", 1.2f), Texte("Action", 1.4f), Texte("Élément", 1.4f), Texte("Identifiant", 2), Texte("Résultat", 0.8f)],
                lignes.Take(AuditMax).Select(a => L(a.DateAction.ToLocalTime(), a.Matricule, a.Module, a.Action, a.Entite, a.EntiteId, a.EstSucces ? "Succès" : "Échec")).ToList())),
        ];
    }

    // ═════ Documents du budget-programme (§ 3.12.2) ════════════════════════════

    private async Task<List<SectionRapport>> DppdAsync(ParametresRapport p)
    {
        if (await CycleAsync(p) is not { } cycle) return [];
        var annees = AnneesCycle(cycle);
        var recap = await _programmation.RecapitulatifAsync(cycle.Id);
        var triennales = annees.Where(a => a >= cycle.AnneeBase).ToArray();

        SectionRapport Section(string titre, Func<MontantsAECP, decimal> mesure)
        {
            List<ColonneRapport> colonnes = [Texte("Programme / action / nature", 3), .. annees.Select(a => Montant(a == cycle.AnneeReference ? $"{a} (réf.)" : a.ToString())), Montant("Total triennal")];
            object?[] Cellules(string libelle, IReadOnlyDictionary<int, MontantsAECP> montants)
                => [libelle, .. annees.Select(a => (object?)mesure(montants.GetValueOrDefault(a))), triennales.Sum(a => mesure(montants.GetValueOrDefault(a)))];

            var lignes = recap.Select(r => r.Action is null && r.Nature is null
                ? Fort(Cellules($"Programme {r.Programme}", r.Montants))
                : Detail(Cellules(r.Action is not null ? $"Action {r.Action}" : $"Nature {r.Nature} ({r.Source})", r.Montants))).ToList();
            var programmes = recap.Where(r => r.Action is null && r.Nature is null).ToList();
            var totaux = annees.ToDictionary(a => a, a => new MontantsAECP(programmes.Sum(r => r.Montants.GetValueOrDefault(a).AE), programmes.Sum(r => r.Montants.GetValueOrDefault(a).CP)));
            return new(titre, new TableauRapport(colonnes, lignes, L(Cellules("Total", totaux))));
        }

        return
        [
            new("Cycle", Texte: $"{cycle.Libelle} : exercice de référence {cycle.AnneeReference}, années {cycle.AnneeBase} à {cycle.AnneeFin}. Total triennal = {cycle.AnneeBase} + {cycle.AnneeBase + 1} + {cycle.AnneeFin}."),
            Section("Autorisations d'engagement (AE)", m => m.AE),
            Section("Crédits de paiement (CP)", m => m.CP),
        ];
    }

    private async Task<List<SectionRapport>> RessourcesAsync(ParametresRapport p)
    {
        if (await CycleAsync(p) is not { } cycle) return [];
        var annees = AnneesCycle(cycle);
        var triennales = annees.Where(a => a >= cycle.AnneeBase).ToArray();
        var ressources = await _programmation.ListerRessourcesAsync(cycle.Id);
        var equilibre = await _programmation.EquilibreAsync(cycle.Id);
        return
        [
            new("Ressources par nature et source", new TableauRapport(
                [Texte("Nature de recette", 3), Texte("Source", 1), .. annees.Select(a => Montant(a.ToString())), Montant("Total triennal")],
                ressources.Select(r => L([r.Nature, r.Source, .. annees.Select(a => (object?)r.Montants.GetValueOrDefault(a)), triennales.Sum(a => r.Montants.GetValueOrDefault(a))])).ToList(),
                L(["Total", null, .. annees.Select(a => (object?)ressources.Sum(r => r.Montants.GetValueOrDefault(a))), ressources.Sum(r => triennales.Sum(a => r.Montants.GetValueOrDefault(a)))]))),
            new("Équilibre ressources / emplois", new TableauRapport(
                [Texte("Année", 1), Montant("Ressources"), Montant("AE"), Montant("CP"), Montant("Solde (ressources − CP)")],
                equilibre.Select(e => L(e.Annee.ToString(), e.Ressources, e.AE, e.CP, e.Solde)).ToList())),
        ];
    }

    private async Task<List<SectionRapport>> IndicateursAsync(ParametresRapport p)
    {
        if (await CycleAsync(p) is not { } cycle) return [];
        var cibles = Enumerable.Range(cycle.AnneeBase, cycle.AnneeFin - cycle.AnneeBase + 1).ToArray();
        var indicateurs = (await _indicateurs.TableauAsync(cycle.Id, p.ProgrammeId)).Where(i => i.EstActif).ToList();
        var lignes = new List<LigneRapport>();
        foreach (var programme in indicateurs.GroupBy(i => i.Programme ?? "Sans programme"))
        {
            lignes.Add(Fort([$"Programme {programme.Key}", null, null, null, null, .. cibles.Select(_ => (object?)null)]));
            lignes.AddRange(programme.Select(i => Detail([$"{i.Code} — {i.Libelle}{(i.Action is null ? "" : $" (action {i.Action})")}",
                LibellesBudgetProgramme.Niveau[i.Niveau], i.Unite, LibellesBudgetProgramme.Sens[i.Sens],
                i.ValeurReference is { } v ? $"{v:N2}{(i.AnneeReference is { } a ? $" ({a})" : "")}" : null,
                .. cibles.Select(a => (object?)(i.Cibles.TryGetValue(a, out var c) ? c : null))])));
        }
        return
        [
            new("Indicateurs et cibles", new TableauRapport(
                [Texte("Indicateur", 4), Texte("Niveau", 1.3f), Texte("Unité", 0.9f), Texte("Sens", 1.2f), Texte("Référence", 1.2f), .. cibles.Select(a => Nombre($"Cible {a}"))],
                lignes)),
        ];
    }

    private async Task<List<SectionRapport>> CadreLogiqueAsync(ParametresRapport p)
    {
        if (await CycleAsync(p) is not { } cycle) return [];
        var programmes = (await _programmes.ListerProgrammesAsync()).Where(x => p.ProgrammeId is null || x.Id == p.ProgrammeId).ToList();
        var sections = new List<SectionRapport>();
        foreach (var programme in programmes)
        {
            var cadre = await _cadreLogique.ObtenirAsync(programme.Id, cycle.Id);
            if (cadre is null || cadre.Elements.Count == 0) continue;

            // Arbre : objectif général > objectifs spécifiques > résultats immédiats.
            var lignes = new List<LigneRapport>();
            void Ajouter(Ulid? parentId, int niveau)
            {
                foreach (var e in cadre.Elements.Where(x => x.ParentId == parentId).OrderBy(x => x.Ordre))
                {
                    lignes.Add(new LigneRapport([LibellesBudgetProgramme.ElementCadre[e.Type], e.Libelle, e.Action, e.Indicateur, e.ValeurCible, e.SourceVerification,
                        string.Join(", ", e.Risques.Select(r => r.Libelle))], niveau, Forte: e.Type == TypeElementCL.ObjectifGeneral));
                    Ajouter(e.Id, niveau + 1);
                }
            }
            Ajouter(null, 0);
            var anomalies = cadre.Anomalies.Count == 0 ? null : "Points à compléter : " + string.Join(" ; ", cadre.Anomalies);
            sections.Add(new($"{programme.Code} — {programme.Libelle}", new TableauRapport(
                [Texte("Niveau", 1.3f), Texte("Élément", 3.5f), Texte("Action", 1), Texte("Indicateur", 2.2f), Nombre("Cible"), Texte("Source de vérification", 1.6f), Texte("Risques", 1.8f)],
                lignes), anomalies));
        }
        return sections.Count > 0 ? sections : [new("Cadre logique", Texte: "Aucun cadre logique saisi pour ce cycle.")];
    }

    private async Task<List<SectionRapport>> EmploisAsync(ParametresRapport p)
    {
        if (await CycleAsync(p) is not { } cycle) return [];
        var annees = AnneesCycle(cycle);
        var emplois = await _programmation.ListerEmploisAsync(cycle.Id);
        var personnel = await _programmation.EffectifsEtPersonnelAsync(cycle.Id);

        var parProgramme = new List<LigneRapport>();
        foreach (var programme in emplois.GroupBy(e => e.Programme))
        {
            parProgramme.Add(Fort([$"Programme {programme.Key}", .. annees.Select(a => (object?)programme.Sum(e => e.Effectifs.GetValueOrDefault(a)))]));
            parProgramme.AddRange(programme.Select(e => Detail([e.Categorie, .. annees.Select(a => (object?)e.Effectifs.GetValueOrDefault(a))])));
        }
        return
        [
            new("Emplois par catégorie (global)", new TableauRapport(
                [Texte("Catégorie d'emplois", 3), .. annees.Select(a => Nombre(a.ToString()))],
                emplois.GroupBy(e => e.Categorie).Select(g => L([g.Key, .. annees.Select(a => (object?)g.Sum(e => e.Effectifs.GetValueOrDefault(a)))])).ToList(),
                L(["Total", .. annees.Select(a => (object?)emplois.Sum(e => e.Effectifs.GetValueOrDefault(a)))]))),
            new("Emplois par programme", new TableauRapport([Texte("Programme / catégorie", 3), .. annees.Select(a => Nombre(a.ToString()))], parProgramme)),
            new("Effectifs et crédits de personnel", new TableauRapport(
                [Texte("Programme", 3), .. annees.SelectMany(a => new[] { Nombre($"Effectif {a}"), Montant($"CP personnel {a}") })],
                personnel.Select(x => L([x.Programme, .. annees.SelectMany(a => { var v = x.Annees.GetValueOrDefault(a); return new object?[] { v.Effectif, v.CPPersonnel }; })])).ToList())),
        ];
    }

    private async Task<List<SectionRapport>> ExecutionTroisExercicesAsync(ParametresRapport p)
    {
        var (lignes, annees) = await _consommation.ExecutionTroisExercicesAsync(p.ExerciceId!.Value);
        object?[] Cellules(string libelle, Func<int, (decimal Prevision, decimal Realise)> valeur)
            => [libelle, .. annees.SelectMany(a => { var v = valeur(a); return new object?[] { v.Prevision, v.Realise, CalculsExecution.Taux(v.Realise, v.Prevision) }; })];
        return
        [
            new("Exécution par nature de dépense", new TableauRapport(
                [Texte("Nature de dépense", 3), .. annees.SelectMany(a => new[] { Montant($"Prévision {a}"), Montant($"Réalisé {a}"), Taux($"Taux {a}") })],
                lignes.Select(l => L(Cellules(l.Nature, a => l.Annees.GetValueOrDefault(a)))).ToList(),
                L(Cellules("Total", a => (lignes.Sum(l => l.Annees.GetValueOrDefault(a).Prevision), lignes.Sum(l => l.Annees.GetValueOrDefault(a).Realise)))))),
            new("Sources", Texte: "Exercice non clôturé : situation courante (prévision ajustée, paiements). Exercice clôturé : historique d'exécution versé à la clôture ou repris."),
        ];
    }

    private async Task<List<SectionRapport>> RapportTrimestrielAsync(ParametresRapport p)
    {
        var f = Filtre(p);
        var periode = (await _exercices.ListerPeriodesAsync(f.ExerciceId)).First(x => x.Id == p.PeriodeId);
        var c = await _consommation.ConsoliderAsync(f);
        if (c is null) return [];
        var natures = await _consommation.RegrouperAsync(f, Regroupement.Nature);
        var trimestres = await _consommation.EvolutionAsync(f, TypePeriode.Trimestre);
        var mensuel = (await _recettes.MensuelAsync(f.ExerciceId)).Where(m => m.Debut >= periode.DateDebut && m.Debut <= periode.DateFin).ToList();
        var trimestre = trimestres.FirstOrDefault(t => t.Numero == periode.Numero);

        var sections = new List<SectionRapport>
        {
            new($"Situation au {periode.DateFin:dd/MM/yyyy}", Chiffres:
            [
                .. ChiffresSituation(c.Total), ("Engagé sur le trimestre", M(trimestre?.Engage)), ("Payé sur le trimestre", M(trimestre?.Paye)),
                ("Paiements projetés en fin d'exercice", M(c.PaiementsProjetes)),
            ]),
            new("Situation par axe", new TableauRapport(ColonnesSituation("Axe"), c.Axes.Select(a => L(CellulesSituation(a.Axe, a.Montants))).ToList(),
                L(CellulesSituation("Total", c.Total)))),
            new("Situation par nature de dépense", TableauRegroupement("Nature de dépense", natures)),
            new("Engagements et paiements par trimestre", new TableauRapport(
                [Texte("Trimestre", 1.2f), Montant("Engagé"), Montant("Payé"), Montant("Engagé cumulé"), Montant("Payé cumulé")],
                trimestres.Select(t => L(t.Libelle, t.Engage, t.Paye, t.EngageCumule, t.PayeCumule)).ToList())),
        };
        if (mensuel.Count > 0)
            sections.Add(new("Recettes du trimestre", new TableauRapport(
                [Texte("Mois", 1.5f), Montant("Encaissé"), Montant("Encaissé N-1")],
                mensuel.Select(m => L(m.Debut.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")), m.Encaisse, m.EncaisseN1)).ToList(),
                L("Total", mensuel.Sum(m => m.Encaisse), mensuel.Sum(m => m.EncaisseN1)))));
        return sections;
    }

    private async Task<List<SectionRapport>> RapportSemestrielAsync(ParametresRapport p)
    {
        var f = Filtre(p);
        var programmes = await _consommation.RegrouperAsync(f, Regroupement.Programme);
        var actions = await _consommation.RegrouperAsync(f, Regroupement.Action);
        var aecp = await _consommation.AECPParProgrammeAsync(f);
        var realisations = await _indicateurs.ListerRealisationsAsync(p.PeriodeId!.Value);
        return
        [
            new("Exécution par programme", TableauRegroupement("Programme", programmes)),
            new("Exécution par action", TableauRegroupement("Action", actions)),
            new("AE et CP programmés / exécutés", TableauAECP(aecp)),
            new("Réalisations des indicateurs sur le semestre", TableauRealisations(realisations)),
        ];
    }

    private async Task<List<SectionRapport>> RapportAnnuelAsync(ParametresRapport p)
    {
        var f = Filtre(p);
        var synthese = await _indicateurs.SyntheseAsync(f.ExerciceId);
        var aecp = await _consommation.AECPParProgrammeAsync(f);
        var annee = (await _exercices.ListerPeriodesAsync(f.ExerciceId)).FirstOrDefault(x => x.Type == TypePeriode.Annee);
        var realisations = annee is null ? [] : await _indicateurs.ListerRealisationsAsync(annee.Id);
        var analyses = await _analyses.ListerAsync(f.ExerciceId);

        var sections = new List<SectionRapport>
        {
            new("Performance par programme", new TableauRapport(
                [Texte("Programme", 3), Nombre("Indicateurs"), Nombre("Mesurés"), Nombre("Atteints"), Taux("Atteinte moyenne")],
                synthese.Select(s => L($"{s.Programme} — {s.Libelle}", s.NbIndicateurs, s.NbMesures, s.NbAtteints, s.TauxAtteinteMoyen)).ToList())),
            new("Coûts programmés et exécutés", TableauAECP(aecp)),
            new("Réalisations annuelles des indicateurs", TableauRealisations(realisations)),
        };
        foreach (var groupe in analyses.GroupBy(a => a.Type).OrderBy(g => g.Key))
            sections.Add(new(LibellesBudgetProgramme.Analyse[groupe.Key],
                Texte: string.Join("\n\n", groupe.OrderBy(a => a.Programme).ThenBy(a => a.Ordre)
                    .Select(a => (a.Programme is null ? "" : $"[{a.Programme}] ") + (a.Rubrique is null ? "" : $"[{a.Rubrique}] ") + a.Contenu))));
        return sections;
    }

    private static TableauRapport TableauAECP(IReadOnlyList<LigneAECP> lignes) => new(
        [Texte("Programme", 3), Montant("AE programmées"), Montant("AE exécutées"), Taux("Taux AE"), Montant("CP programmés"), Montant("CP exécutés"), Taux("Taux CP")],
        lignes.Select(l => L(l.Programme, l.AEProgramme, l.AEExecute, l.TauxAE, l.CPProgramme, l.CPExecute, l.TauxCP)).ToList(),
        L("Total", lignes.Sum(l => l.AEProgramme), lignes.Sum(l => l.AEExecute), CalculsExecution.Taux(lignes.Sum(l => l.AEExecute), lignes.Sum(l => l.AEProgramme)),
            lignes.Sum(l => l.CPProgramme), lignes.Sum(l => l.CPExecute), CalculsExecution.Taux(lignes.Sum(l => l.CPExecute), lignes.Sum(l => l.CPProgramme))));

    private static TableauRapport TableauRealisations(IReadOnlyList<LigneRealisation> lignes) => new(
        [Texte("Indicateur", 4), Texte("Programme / action", 1.5f), Texte("Unité", 0.9f), Nombre("Cible"), Nombre("Réalisé"), Taux("Atteinte"), Texte("Statut", 1)],
        lignes.Select(r => L($"{r.Code} — {r.Libelle}", r.Action ?? r.Programme, r.Unite, r.Cible, r.Valeur, r.TauxAtteinte,
            r.Statut is { } s ? LibellesExecution.Statut[s] : "Non saisi")).ToList());
}
