using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Securite;
using eProcessBudget.Infrastructure.Services.Execution;
using eProcessBudget.Infrastructure.Services.Recettes;

namespace eProcessBudget.Web.Components.Layout;

/// <summary>
/// Une entrée de la barre latérale.
/// </summary>
/// <param name="Href">Route de la page, telle que déclarée par son @page.</param>
/// <param name="Icone">Nom d'icône reconnu par AppIcon.</param>
/// <param name="Label">Texte affiché quand la barre est dépliée.</param>
/// <param name="Droits">
/// Droits dont AU MOINS UN est nécessaire pour voir l'entrée.
/// null ou vide = visible par tout utilisateur.
/// </param>
public record EntreeMenu(string Href, string Icone, string Label, TypeDroit[]? Droits = null);

/// <summary>
/// Une section de la barre latérale : un titre et ses entrées. Un clic sur le titre
/// déplie la section (et replie les autres) ; celle de la page courante s'ouvre seule.
/// Une section dont aucune entrée n'est visible disparaît entièrement, titre compris.
/// </summary>
/// <param name="Titre">null pour la section principale, toujours dépliée et sans titre.</param>
/// <param name="Icone">Icône du titre (nom reconnu par AppIcon).</param>
public record SectionMenu(string? Titre, EntreeMenu[] Entrees, string Icone = "collection");

/// <summary>
/// ═══════════════════════════════════════════════════════════════════════════
///  LE MENU DE L'APPLICATION SE DÉFINIT ICI, ET NULLE PART AILLEURS.
/// ═══════════════════════════════════════════════════════════════════════════
///
/// Pour ajouter une page au menu :
///   1. créez la page dans Features/VotreDomaine/Index.razor avec son @page ;
///   2. ajoutez une EntreeMenu ci-dessous, avec les droits qui la conditionnent ;
///   3. c'est tout — MainLayout filtre automatiquement selon les droits de l'utilisateur.
///
/// RÈGLE DE SÉCURITÉ : masquer une entrée de menu n'est PAS une protection. C'est du
/// confort d'affichage. La page elle-même doit se protéger avec &lt;AutoriseSi&gt; —
/// voir docs/05-droits-acces.md.
/// </summary>
public static class MenuApplication
{
    /// <summary>Quiconque consulte ou saisit dans le budget-programme.</summary>
    private static readonly TypeDroit[] LectureBudgetProgramme =
    [
        TypeDroit.BudgetProgrammeLire, TypeDroit.BudgetProgrammeDefinir,
        TypeDroit.ProgrammationSaisir, TypeDroit.IndicateurSaisirRealisation
    ];

    public static readonly SectionMenu[] Sections =
    {
        // ── Section principale, sans titre ───────────────────────────────────
        new(null, new EntreeMenu[]
        {
            new("/",          "home",       "Accueil"),
            new("/notifications", "bell",   "Notifications"),
            new("/aide",      "information-circle", "Guide d'utilisation"),
        }),

        // ── Plateforme (Administrateur plateforme) ───────────────────────────
        new("Plateforme", new EntreeMenu[]
        {
            new("/plateforme/organismes", "office-building", "Organismes",
                new[] { TypeDroit.OrganismeGerer }),
        }, Icone: "office-building"),

        // ── Pilotage (cahier technique § 4.7) ; le tableau de bord est la page d'accueil ──
        new("Pilotage", new EntreeMenu[]
        {
            new("/consommation", "chart-bar", "Consommation budgétaire", new[] { TypeDroit.ConsommationLire }),
            new("/rapports", "document-text", "Rapports et exports", new[] { TypeDroit.RapportExporter }),
        }, Icone: "chart-bar"),

        // ── Budget-programme et performance (cahier technique § 4.3) ─────────
        new("Budget-programme", new EntreeMenu[]
        {
            new("/budget-programme", "clipboard-list", "Programmes", LectureBudgetProgramme),
            new("/budget-programme/indicateurs", "chart-bar", "Indicateurs", LectureBudgetProgramme),
            new("/budget-programme/tableau-indicateurs", "collection", "Tableau des cibles", LectureBudgetProgramme),
            new("/budget-programme/cadre-logique", "document-text", "Cadre logique", LectureBudgetProgramme),
            new("/budget-programme/programmation", "clock", "Programmation", LectureBudgetProgramme),
            new("/budget-programme/repartitions", "users", "Répartitions", LectureBudgetProgramme),
            new("/budget-programme/performance", "check-circle", "Performance", LectureBudgetProgramme),
            new("/budget-programme/analyses", "sparkles", "Analyses", LectureBudgetProgramme),
        }, Icone: "clipboard-list"),

        // ── Cycle budgétaire commun (cahier technique § 4.4) ─────────────────
        new("Exécution budgétaire", new EntreeMenu[]
        {
            new("/execution", "collection", "Dotations", AccesExecution.Lecture),
            new("/execution/ajustements", "adjustments", "Ajustements", AccesExecution.Lecture),
            new("/execution/engagements", "clipboard-list", "Engagements", AccesExecution.Lecture),
            new("/execution/depassements", "scale", "Dépassements", AccesExecution.Lecture),
            new("/execution/paiements", "cash", "Paiements", AccesExecution.Lecture),
            new("/execution/cloture", "lock-closed", "Clôture", AccesExecution.Lecture),
        }, Icone: "cash"),

        // ── Axes de dépenses (cahier technique § 4.5) ────────────────────────
        new("Axes de dépenses", new EntreeMenu[]
        {
            new("/axes/entretien", "clipboard-list", "Entretien routier", AxesDepense.Entretien.Lecture),
            new("/axes/entretien/contrats", "document-text", "Contrats d'entretien", AxesDepense.Entretien.Lecture),
            new("/axes/entretien/avancement", "chart-bar", "Avancement physique", AxesDepense.Entretien.Lecture),
            new("/axes/investissements", "office-building", "Investissements", AxesDepense.Investissement.Lecture),
            new("/axes/investissements/inventaire", "collection", "Inventaire", AxesDepense.Investissement.Lecture),
            new("/axes/investissements/sites", "scale", "Comparaison inter-sites", AxesDepense.Investissement.Lecture),
            new("/axes/fonctionnement", "cog", "Fonctionnement", AxesDepense.Fonctionnement.Lecture),
            new("/axes/appuis", "users", "Appuis", AxesDepense.Appuis.Lecture),
            new("/axes/appuis/versements", "cash", "Versements d'appuis", AxesDepense.Appuis.Lecture),
        }, Icone: "chart-bar"),

        // ── Recettes (cahier technique § 4.6) ────────────────────────────────
        new("Recettes", new EntreeMenu[]
        {
            new("/recettes", "chart-bar", "Synthèse", AccesRecettes.Lecture),
            new("/recettes/previsions", "collection", "Prévisions", AccesRecettes.Lecture),
            new("/recettes/encaissements", "cash", "Encaissements", AccesRecettes.Lecture),
            new("/recettes/factures-emises", "document-text", "Factures émises", AccesRecettes.Lecture),
            new("/recettes/echeancier", "clock", "Échéancier", AccesRecettes.Lecture),
            new("/recettes/reports", "arrow-right", "Reports de ressources", AccesRecettes.Lecture),
            new("/recettes/bilan", "scale", "Bilan recettes / dépenses", AccesRecettes.Lecture),
        }, Icone: "cash"),

        // ── Paramétrage (cahier technique § 4.2) ─────────────────────────────
        new("Paramétrage", new EntreeMenu[]
        {
            new("/parametrage", "cog", "Vue d'ensemble",
                new[] { TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer }),
            new("/parametrage/organisme", "office-building", "Organisme",
                new[] { TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer }),
            new("/parametrage/exercices", "clock", "Exercices et cycles",
                new[] { TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer }),
            new("/parametrage/nomenclature", "collection", "Nomenclature",
                new[] { TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer }),
            new("/parametrage/import", "download", "Import Excel",
                new[] { TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer }),
            new("/parametrage/historique", "clock", "Reprise de l'historique",
                new[] { TypeDroit.ParametrageLire, TypeDroit.ParametrageGerer }),
        }, Icone: "cog"),

        // ── Administration ───────────────────────────────────────────────────
        new("Administration", new EntreeMenu[]
        {
            new("/administration/utilisateurs", "users", "Utilisateurs",
                new[] { TypeDroit.UtilisateurConsultation, TypeDroit.UtilisateurGestion }),

            new("/administration/habilitations", "key", "Habilitations",
                new[] { TypeDroit.HabilitationDemander, TypeDroit.HabilitationValiderFinancier,
                        TypeDroit.HabilitationValiderAdminSI }),

            new("/administration/profils", "shield-check", "Profils et droits",
                new[] { TypeDroit.ProfilConsultation, TypeDroit.ProfilGestion }),

            new("/administration/notifications", "cog", "Canaux de notification",
                new[] { TypeDroit.NotificationParametrage }),

            new("/administration/connexions", "clock", "Journal des connexions",
                new[] { TypeDroit.AuditConsultation }),

            new("/administration/audit", "search", "Journal d'audit",
                new[] { TypeDroit.AuditConsultation }),
        }, Icone: "shield-check"),

        // ── VOTRE MÉTIER : ajoutez vos sections ici ──────────────────────────
        // new("Budget", new EntreeMenu[]
        // {
        //     new("/budgets", "clipboard-list", "Budgets",
        //         new[] { TypeDroit.BudgetConsultation }),
        // }),
    };

    /// <summary>Toutes les entrées, toutes sections confondues.</summary>
    public static IEnumerable<EntreeMenu> ToutesLesEntrees()
        => Sections.SelectMany(s => s.Entrees);

    /// <summary>
    /// Routes des entrées que l'utilisateur peut voir (un seul de leurs droits suffit).
    /// Sert au menu et au guide d'utilisation, qui ne documente que ces écrans.
    /// </summary>
    public static async Task<HashSet<string>> RoutesAutoriseesAsync(IDroitService droits, Ulid userId)
    {
        IReadOnlySet<TypeDroit> effectifs = userId == Ulid.Empty
            ? new HashSet<TypeDroit>()
            : await droits.ObtenirDroitsAsync(userId);

        return ToutesLesEntrees()
            .Where(e => e.Droits is not { Length: > 0 } || e.Droits.Any(effectifs.Contains))
            .Select(e => e.Href)
            .ToHashSet();
    }
}


