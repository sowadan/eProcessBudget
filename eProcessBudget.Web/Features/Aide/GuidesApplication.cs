using eProcessBudget.Web.Components.Layout;

namespace eProcessBudget.Web.Features.Aide;

/// <summary>
/// Une étape de visite guidée.
/// </summary>
/// <param name="Element">
/// Élément mis en évidence (voir wwwroot/js/guide.js) : sélecteur CSS, « carte:sélecteur »
/// (la carte qui le contient), « groupe:sélecteur » (son parent), ou null pour une bulle
/// centrée. Une étape dont l'élément est absent de la page est sautée.
/// </param>
/// <param name="Titre">Titre de la bulle ; vide = titre de la page.</param>
/// <param name="Texte">Texte de la bulle. Balises simples admises (strong, em, br).</param>
/// <param name="Cote">Côté de la bulle : haut, droite, bas, gauche ; null = automatique.</param>
public sealed record EtapeGuide(string? Element, string Titre, string Texte, string? Cote = null);

/// <summary>
/// Le guide d'un écran : le texte du manuel et la visite guidée qui le joue dans la page.
/// </summary>
/// <param name="Route">Route de l'écran ; un segment {Parametre} accepte toute valeur.</param>
/// <param name="Titre">Titre du guide dans le manuel.</param>
/// <param name="Resume">Ce que permet l'écran : première bulle de la visite.</param>
/// <param name="BonASavoir">Règles et enchaînements propres à l'écran : dernières bulles.</param>
/// <param name="Actions">Texte de la bulle « boutons d'action », s'il diffère du texte commun.</param>
/// <param name="Tableau">Texte de la bulle « liste », s'il diffère du texte commun.</param>
public sealed record GuideUtilisation(
    string Route,
    string Titre,
    string Resume,
    EtapeGuide[] BonASavoir,
    string? Actions = null,
    string? Tableau = null)
{
    /// <summary>
    /// Toutes les étapes, dans l'ordre de la visite : présentation, éléments communs à
    /// tous les écrans (sautés s'ils n'y figurent pas), puis les règles propres à l'écran.
    /// </summary>
    public IEnumerable<EtapeGuide> Etapes()
    {
        yield return new("[data-guide=\"page-entete\"]", Titre, Resume, "bas");
        foreach (var etape in GuidesApplication.EtapesCommunes(Actions, Tableau)) yield return etape;
        foreach (var etape in BonASavoir) yield return etape;
    }

    /// <summary>Forme transmise à guide.js.</summary>
    public object PourNavigateur() => new { Titre, Etapes = Etapes().ToArray() };
}

/// <summary>
/// ═══════════════════════════════════════════════════════════════════════════
///  LE GUIDE D'UTILISATION SE RÉDIGE ICI.
/// ═══════════════════════════════════════════════════════════════════════════
///
/// Chaque écran a son guide, qui sert deux fois :
///   - la page « Guide d'utilisation » (/aide) l'affiche comme un manuel, filtré selon les
///     droits de l'utilisateur (un guide suit les droits de l'entrée de menu de son écran) ;
///   - le bouton d'aide de l'en-tête le joue en visite guidée sur l'écran courant.
///
/// Un écran sans guide reçoit la visite générique : en-tête, actions, filtres, onglets,
/// liste. Pour documenter un nouvel écran, ajoutez un GuideUtilisation ci-dessous ; pour
/// cibler un élément précis, posez-lui un attribut data-guide="..." et désignez-le par
/// [data-guide="..."]. Voir docs/20-guide-utilisation.md.
/// </summary>
public static class GuidesApplication
{
    /// <summary>Clé de la visite de l'interface (paramètre ?visite=interface).</summary>
    public const string CleInterface = "interface";

    /// <summary>Clé de la visite de l'écran courant (paramètre ?visite=page).</summary>
    public const string ClePage = "page";

    // ═════════════════════════════════════════════════════════════════════
    //  Éléments communs à tous les écrans
    // ═════════════════════════════════════════════════════════════════════

    internal static IEnumerable<EtapeGuide> EtapesCommunes(string? actions, string? tableau) =>
    [
        new("[data-guide=\"page-actions\"]", "Actions",
            actions ?? "Les actions de l'écran : créer, importer, exporter… Elles n'apparaissent que si vos droits le permettent.",
            "bas"),
        new("groupe:[data-guide=\"indicateur\"]", "Chiffres clés",
            "Les indicateurs principaux de l'écran, recalculés selon les filtres choisis."),
        new("carte:main .mud-input-control", "Filtres",
            "Affinez l'affichage : la liste se met à jour dès qu'un critère change. Videz un champ (croix) pour lever le filtre."),
        new("main .mud-tabs-tabbar", "Onglets",
            "L'écran est organisé en onglets : chacun présente une vue différente des mêmes données."),
        new("main .mud-table, main .mud-simple-table", "Liste",
            tableau ?? "Les éléments correspondant à vos critères. Les icônes en bout de ligne ouvrent la modification ou les actions disponibles."),
        new("main .mud-pagination", "Pagination",
            "Les résultats sont paginés côté serveur : passez d'une page à l'autre ici."),
    ];

    // ═════════════════════════════════════════════════════════════════════
    //  Visite de l'interface (lancée à la première connexion)
    // ═════════════════════════════════════════════════════════════════════

    public static object VisiteInterface(string nomApplication) => new
    {
        Titre = "Découvrir l'interface",
        Etapes = new EtapeGuide[]
        {
            new(null, $"Bienvenue dans {nomApplication}",
                "Cette visite présente l'interface en une minute.<br>Avancez avec les boutons ou les flèches du clavier ; <strong>Échap</strong> pour quitter à tout moment."),
            new("[data-guide=\"menu\"]", "Le menu",
                "Les modules de l'application, regroupés en sections. Vous ne voyez que les écrans auxquels vos droits donnent accès. Un clic sur le titre d'une section la déplie.",
                "droite"),
            new("[data-guide=\"menu-replier\"]", "Replier le menu",
                "Réduit le menu à ses icônes pour libérer de la place à l'écran ; survolez une icône pour lire son libellé.",
                "droite"),
            new("[data-guide=\"page-entete\"]", "L'en-tête de l'écran",
                "Le fil d'Ariane (pour remonter d'un niveau), le titre de l'écran et, à droite, ses boutons d'action.",
                "bas"),
            new("[data-guide=\"notifications\"]", "Notifications",
                "Les validations à faire, alertes de seuil et décisions qui vous concernent arrivent ici en temps réel. Le badge compte les notifications non lues.",
                "bas"),
            new("[data-guide=\"aide\"]", "Aide",
                "À tout moment : la visite guidée de l'écran affiché, cette visite de l'interface, et le guide d'utilisation complet.",
                "bas"),
            new("[data-guide=\"utilisateur\"]", "Votre compte",
                "Changer votre mot de passe ou vous déconnecter. Sans activité pendant la durée fixée par votre organisme, la session se ferme d'elle-même.",
                "droite"),
        }
    };

    // ═════════════════════════════════════════════════════════════════════
    //  Guides des écrans
    // ═════════════════════════════════════════════════════════════════════

    public static readonly GuideUtilisation[] Tous =
    [
        // ── Section principale ───────────────────────────────────────────
        new("/", "Accueil et tableau de bord",
            "Avec le droit de lecture du tableau de bord, l'accueil présente l'état du budget : taux globaux, volets par axe, évolution mensuelle, alertes de seuil, recettes. Sinon, il présente l'état de votre compte.",
            [
                new(null, "Filtres du tableau de bord",
                    "Exercice, situation <strong>à fin de</strong> mois, trimestre ou semestre, axe, programme, site… Sans période, la situation est celle du jour. Les chiffres ne portent que sur votre périmètre."),
                new(null, "Fraîcheur des chiffres",
                    "Le tableau de bord est recalculé au plus toutes les cinq minutes : une saisie toute récente peut mettre quelques minutes à y apparaître."),
            ]),

        new("/notifications", "Notifications",
            "L'historique de toutes vos notifications : demandes de validation, décisions, alertes de seuil, échéances de recouvrement.",
            [
                new(null, "Traiter une notification",
                    "« Ouvrir » mène à l'écran concerné. En bout de ligne : marquer lue ou non lue, supprimer. En tête d'écran : tout marquer lu, supprimer les notifications lues."),
            ]),

        new("/aide", "Guide d'utilisation",
            "Le manuel de l'application : un guide par écran, limité aux écrans auxquels vous avez accès.",
            [
                new(null, "Visites guidées",
                    "« Lancer la visite » ouvre l'écran et vous le fait parcourir élément par élément. Le bouton d'aide de l'en-tête lance celle de l'écran affiché."),
            ]),

        new("/compte/mot-de-passe", "Mon mot de passe",
            "Changez votre mot de passe : saisissez l'actuel, puis le nouveau deux fois.",
            [
                new(null, "Règles", "Le nouveau mot de passe doit respecter la politique de sécurité de votre organisme."),
            ]),

        // ── Plateforme ───────────────────────────────────────────────────
        new("/plateforme/organismes", "Organismes",
            "Réservé à l'Administrateur plateforme : création des organismes, chacun cloisonné avec ses propres données, utilisateurs et paramètres.",
            [
                new(null, "Créer un organisme",
                    "La création installe les données initiales (devise, seuils, axes, natures, structures…). Créez ensuite son administrateur dans <em>Administration › Utilisateurs</em>."),
                new(null, "Suspendre",
                    "Un organisme suspendu n'est plus accessible à ses utilisateurs ; ses données sont conservées."),
            ],
            Actions: "« Nouvel organisme » crée un organisme et ses données initiales."),

        // ── Pilotage ─────────────────────────────────────────────────────
        new("/consommation", "Consommation budgétaire",
            "La vision consolidée de tous les axes de dépenses : prévisions initiales et ajustées, engagements, factures, paiements, reliquats, projection de fin d'exercice.",
            [
                new(null, "Projection et dérapages",
                    "La projection prolonge le rythme de consommation jusqu'à la fin de l'exercice. Un axe ou une ligne marqué « Dérapage » dépasserait sa dotation à ce rythme."),
                new(null, "Périmètre", "Vous ne voyez que les dotations couvertes par vos habilitations de lecture."),
            ],
            Tableau: "Le tableau consolidé : une ligne par axe, le total en bas, et les barres de taux d'engagement et de projection."),

        new("/rapports", "Rapports et exports",
            "Les états de gestion budgétaire et les documents du budget-programme, en Excel, en PDF ou à l'impression.",
            [
                new(null, "Produire un état",
                    "Choisissez l'état, renseignez ses paramètres (exercice, cycle, période…), puis le format. Seuls les états dont vous pouvez lire les données sont proposés."),
                new(null, "Traçabilité", "Chaque export est inscrit au journal d'audit, avec le format et les filtres utilisés."),
            ]),

        // ── Budget-programme ─────────────────────────────────────────────
        new("/budget-programme", "Programmes",
            "L'arborescence programme › action › activité, avec leurs responsables et structures (la principale est marquée d'une étoile).",
            [
                new(null, "Saisie", "Programmes, actions et activités se gèrent avec le droit « Définir le budget-programme » ; ils s'importent aussi depuis Excel, dans cet ordre."),
            ]),

        new("/budget-programme/{Referentiel}", "Référentiels du budget-programme",
            "Programmes, actions et activités en liste : recherche, création, modification, export et import Excel.",
            [
                new(null, "Ordre d'import", "Importez d'abord les programmes, puis les actions, puis les activités : chacun référence le niveau supérieur par son code."),
            ]),

        new("/budget-programme/indicateurs", "Indicateurs",
            "Les indicateurs de performance, leur valeur de référence et leurs cibles.",
            [
                new(null, "Cibles", "L'action <strong>Cibles</strong> d'un indicateur fixe une valeur pour chaque année du cycle. Un cycle ne peut être validé que si chaque indicateur a sa référence et toutes ses cibles."),
            ]),

        new("/budget-programme/tableau-indicateurs", "Tableau des cibles",
            "Les cibles de chaque indicateur sur les années du cycle choisi, en un seul tableau.",
            []),

        new("/budget-programme/cadre-logique", "Cadre logique",
            "Objectifs, résultats, indicateurs objectivement vérifiables, sources de vérification, hypothèses et risques de chaque élément.",
            [
                new(null, "Anomalies", "Les anomalies signalées (élément sans indicateur, sans source…) bloquent la validation du cycle : corrigez-les d'abord."),
                new(null, "Cycle brouillon", "Le cadre logique ne se modifie que sur un cycle au statut Brouillon."),
            ]),

        new("/budget-programme/programmation", "Programmation",
            "La programmation triennale : AE et CP par ligne et par année, ressources par nature, emplois par catégorie, avec le récapitulatif et l'équilibre.",
            [
                new(null, "Règle AE / CP", "Année après année, les CP cumulés ne peuvent dépasser les AE cumulées."),
                new(null, "Cycle brouillon", "La saisie n'est ouverte que sur un cycle Brouillon, et dans votre périmètre d'habilitation."),
            ]),

        new("/budget-programme/repartitions", "Répartitions",
            "Les clés de répartition des lignes entre activités et le temps des agents, qui servent à ventiler les charges.",
            [
                new(null, "Contrôles", "Les clés d'une ligne totalisent exactement 100 % (ou aucune clé) ; le temps d'un agent ne dépasse pas 100 %."),
            ]),

        new("/budget-programme/performance", "Performance",
            "Les réalisations des indicateurs sur l'exercice, leur taux d'atteinte et leur validation.",
            [
                new(null, "Justification", "Une cible non atteinte exige une justification. Une réalisation validée n'est plus modifiable sans réouverture, qui est tracée."),
            ]),

        new("/budget-programme/analyses", "Analyses",
            "Les analyses qualitatives : contraintes, priorités, défis, recommandations et commentaires par rubrique.",
            []),

        // ── Exécution budgétaire ─────────────────────────────────────────
        new("/execution", "Dotations",
            "Les dotations de l'exercice et leur situation : prévision initiale, ajustements, engagements, paiements, reliquat.",
            [
                new(null, "Prévision initiale", "Libre tant que l'exercice est en préparation, elle est <strong>figée à son ouverture</strong> : elle n'évolue ensuite que par ajustement."),
                new(null, "Fiche", "Un clic sur une dotation ouvre sa fiche : situation complète, engagements, ajustements et reports reçus."),
            ],
            Actions: "« Nouvelle dotation » saisit une prévision initiale (droit « Saisir les dotations »)."),

        new("/execution/dotations/{Id}", "Fiche d'une dotation",
            "La situation complète d'une dotation : prévisions, engagements, factures, paiements, encours et reliquat, ainsi que les pièces qui l'alimentent.",
            [
                new(null, "Pièces validées", "Seuls les ajustements, engagements et paiements <strong>validés</strong> entrent dans la situation."),
            ]),

        new("/execution/ajustements", "Ajustements",
            "Les hausses et baisses de dotation, leur validation et leur rejet.",
            [
                new(null, "Saisir un ajustement", "Montant non nul, <strong>motif et justification obligatoires</strong> ; le motif doit correspondre au sens (hausse ou baisse)."),
                new(null, "Baisses", "Une baisse ne peut ramener la dotation sous les engagements déjà validés."),
            ]),

        new("/execution/engagements", "Engagements",
            "Les autorisations d'engagement (AE) et de dépense (AD) de l'exercice, leur validation et leur paiement.",
            [
                new(null, "Cycle d'un engagement", "Brouillon → Soumis → Validé (ou Rejeté) par le Responsable Budget. Un engagement validé peut être annulé tant qu'il n'a ni paiement, ni facture, ni rejet."),
                new(null, "Dépassement", "Un engagement qui dépasse la dotation n'est pas bloqué : un motif est demandé à la soumission et le Responsable Budget décide avant la validation."),
            ],
            Actions: "« Nouvel engagement » apparaît si vous avez le droit de saisie et que l'exercice est ouvert.",
            Tableau: "Les engagements filtrés ; un clic ouvre la fiche : soumission, validation, factures, paiements, échéancier des CP."),

        new("/execution/engagements/{Id}", "Fiche d'un engagement",
            "Tout le suivi d'un engagement : soumission, validation, factures reçues, paiements, rejets et pénalités, échéancier des CP.",
            [
                new(null, "Paiements", "Un paiement porte sur une facture reçue et ne dépasse pas le reste à payer. Il ne se supprime pas : il s'annule avec un motif."),
                new(null, "Échéancier", "La somme des CP de l'échéancier ne dépasse pas le montant engagé."),
            ]),

        new("/execution/depassements", "Dépassements",
            "La file des dépassements en attente de décision du Responsable Budget.",
            [
                new(null, "Décider", "Accepter valide l'engagement ; refuser le rejette. La décision est notifiée à l'auteur."),
            ]),

        new("/execution/paiements", "Paiements",
            "L'historique des paiements par ligne, projet, entité et période.",
            []),

        new("/execution/cloture", "Clôture",
            "La clôture de l'exercice : aperçu des reliquats, encours et blocages, puis clôture en une opération.",
            [
                new(null, "Préalables", "Exercice ouvert, exercice suivant créé, et aucune pièce en attente (ajustement soumis, engagement en brouillon ou soumis, dépassement non décidé)."),
                new(null, "Ce que fait la clôture", "Reporte les encours et les reliquats sur l'exercice suivant, verse l'exécution à l'historique et clôture l'exercice. L'opération est journalisée et irréversible."),
            ]),

        // ── Axes de dépenses ─────────────────────────────────────────────
        new("/axes/entretien", "Entretien routier",
            "Les chiffres clés de l'axe, le kilométrage prévu et entretenu par type de travaux, et les opérations d'entretien.",
            [
                new(null, "Saisie", "Les opérations se saisissent avec le droit de saisie de l'axe et une habilitation couvrant l'axe, le service ou le site. Leurs dépenses passent par le cycle commun (dotations, engagements, paiements)."),
            ]),

        new("/axes/entretien/contrats", "Contrats d'entretien",
            "Les contrats de travaux et de prestations : montant engagé, payé, kilomètres réalisés face aux kilomètres contractuels.",
            []),

        new("/axes/entretien/avancement", "Avancement physique",
            "Les relevés mensuels : kilomètres entretenus et taux d'avancement, par contrat et type de travaux.",
            []),

        new("/axes/investissements", "Investissements",
            "Les projets d'investissement (source RI ou RE) et leur situation par site, service, rubrique et nature.",
            []),

        new("/axes/investissements/inventaire", "Inventaire",
            "Les biens immobilisés par site, compte d'immobilisation, projet d'acquisition et état.",
            []),

        new("/axes/investissements/sites", "Comparaison inter-sites",
            "Taux d'engagement et de consommation par site, et valeur de l'inventaire de chacun.",
            []),

        new("/axes/fonctionnement", "Fonctionnement",
            "L'axe Fonctionnement par activité (via les clés de répartition), site, service et nature ; les lignes proches du dépassement ; le personnel et les effectifs.",
            [
                new(null, "Saisie", "Cet axe n'a pas d'objet propre : tout passe par les dotations et engagements du cycle commun."),
            ]),

        new("/axes/appuis", "Appuis",
            "Les appuis par entité bénéficiaire (hiérarchie, sites de la zone d'implantation, solde), les projets d'appui et la répartition par rubrique.",
            []),

        new("/axes/appuis/versements", "Versements d'appuis",
            "L'historique des versements par entité et par projet, et les appuis en attente de versement.",
            []),

        new("/axes/projets/{Id}", "Fiche d'un projet",
            "Identité du projet, situation sur l'exercice, dotations, engagements, paiements, contrats, avancement ou immobilisations.",
            [
                new(null, "Navigation", "Un clic sur une dotation, un engagement ou un paiement ouvre l'écran correspondant de l'exécution budgétaire."),
            ]),

        // ── Recettes ─────────────────────────────────────────────────────
        new("/recettes", "Synthèse des recettes",
            "Les indicateurs clés et les encaissements mensuels face à l'exercice précédent et à l'échéancier, par catégorie, nature, site et collecteur.",
            []),

        new("/recettes/previsions", "Prévisions de recettes",
            "Les prévisions par ligne, nature, site et collecteur, leurs révisions et leur échéancier de recouvrement.",
            [
                new(null, "Révision", "Une révision remplace la prévision initiale dans les calculs ; l'initiale reste visible pour comparaison."),
            ]),

        new("/recettes/encaissements", "Encaissements",
            "La saisie mensuelle des encaissements (grille prévision × mois) et leur journal.",
            []),

        new("/recettes/factures-emises", "Factures émises",
            "Les droits constatés par prévision de recette, et leur recouvrement par les encaissements.",
            []),

        new("/recettes/echeancier", "Échéancier de recouvrement",
            "Toutes les échéances de l'exercice : couvertes, à venir ou en retard. Les échéances dépassées font l'objet d'une alerte.",
            []),

        new("/recettes/reports", "Reports de ressources",
            "Les reliquats de ressources des exercices passés, positionnés ou non sur l'exercice.",
            []),

        new("/recettes/bilan", "Bilan recettes / dépenses",
            "Les ressources face aux dépenses, en prévision et en réalisation, et la trésorerie positionnée ou non.",
            []),

        // ── Paramétrage ──────────────────────────────────────────────────
        new("/parametrage", "Paramétrage — vue d'ensemble",
            "L'accès à tous les paramètres et référentiels de l'organisme. Aucune saisie budgétaire n'est possible sur un élément non paramétré.",
            []),

        new("/parametrage/{Referentiel}", "Référentiels",
            "Chaque référentiel se gère de la même façon : recherche, filtre actif / inactif, création, modification, activation, suppression, export et import Excel.",
            [
                new(null, "Désactiver ou supprimer", "Un élément utilisé ne peut pas être supprimé : désactivez-le. La désactivation est refusée tant que des éléments actifs en dépendent."),
                new(null, "Excel", "« Exporter » produit le modèle (en-têtes, éléments existants, feuille d'aide). Complétez-le puis « Importer » : tout ou rien, avec un rapport ligne par ligne en cas d'erreur."),
            ],
            Actions: "Exporter le référentiel en Excel ; avec le droit de gestion, l'importer ou ajouter un élément."),

        new("/parametrage/organisme", "Paramètres de l'organisme",
            "Identité, logo, devise de référence, durée de session, tentatives de connexion, pagination, rétention des journaux, en-tête et pied des rapports.",
            [
                new(null, "Données initiales", "« Compléter les données initiales » ajoute les éléments standard manquants, sans modifier l'existant."),
            ]),

        new("/parametrage/exercices", "Exercices et cycles",
            "Les exercices budgétaires (périodes, ouverture, clôture des mois) et les cycles de programmation triennaux.",
            [
                new(null, "Ouvrir un exercice", "L'ouverture fige la prévision initiale des dotations. Les périodes se clôturent ou se rouvrent sur un exercice ouvert."),
                new(null, "Valider un cycle", "Refusé tant qu'un indicateur n'a pas sa référence et ses cibles, ou que le cadre logique présente des anomalies."),
            ]),

        new("/parametrage/nomenclature", "Nomenclature budgétaire",
            "L'arborescence axe › rubrique › sous-rubrique › ligne, et les comptes d'imputation de chaque ligne.",
            [
                new(null, "Compte principal", "Chaque ligne budgétaire doit avoir au moins un compte d'imputation, dont un principal : l'absence est signalée dans l'arborescence."),
            ]),

        new("/parametrage/import", "Import Excel",
            "Le modèle et l'import de chaque référentiel, présentés dans l'ordre des dépendances.",
            [
                new(null, "Ordre", "Suivez l'ordre proposé : un référentiel désigne les autres par leur code, qui doit déjà exister."),
                new(null, "Limites", "Fichiers .xlsx de 5 Mo et 5 000 lignes au plus. Une seule ligne en erreur et rien n'est enregistré."),
            ]),

        new("/parametrage/seuils", "Seuils d'alerte",
            "Les paliers de consommation qui déclenchent une alerte (80 / 90 / 100 % par défaut) : globaux, par axe ou par ligne budgétaire.",
            [
                new(null, "Priorité", "Les seuils d'une ligne priment sur ceux de son axe, qui priment sur les seuils globaux. Chaque seuil n'alerte qu'une fois par dotation."),
            ]),

        new("/parametrage/historique", "Reprise de l'historique",
            "L'exécution des cinq exercices antérieurs à la mise en service, reprise par import Excel : téléchargez le modèle, complétez-le, importez-le.",
            []),

        // ── Administration ───────────────────────────────────────────────
        new("/administration/utilisateurs", "Utilisateurs",
            "Les comptes de l'organisme : création, modification, activation, déblocage, réinitialisation du mot de passe et attribution des profils.",
            [
                new(null, "Compte bloqué", "Après trop d'échecs de connexion, un compte est bloqué : le bouton « Débloquer » de sa ligne le rétablit."),
            ]),

        new("/administration/habilitations", "Habilitations",
            "Les droits accordés sur un périmètre (organisme, programme, axe, structure, site), actifs après double validation.",
            [
                new(null, "Double validation", "Une habilitation demandée ne prend effet qu'après deux validations par deux personnes distinctes : le validateur financier et l'Administrateur SI. Elle s'applique à tous les niveaux d'accès, lecture comprise."),
            ]),

        new("/administration/profils", "Profils et droits",
            "Les profils regroupent des droits réutilisables ; un utilisateur reçoit un ou plusieurs profils, plus d'éventuels droits directs.",
            []),

        new("/administration/notifications", "Canaux de notification",
            "Pour chaque type d'événement : notification dans l'application, par email, ou les deux.",
            []),

        new("/administration/connexions", "Journal des connexions",
            "Les connexions, échecs, blocages et déconnexions des comptes de l'organisme.",
            []),

        new("/administration/audit", "Journal d'audit",
            "Qui a fait quoi et quand : créations, modifications et suppressions avec les valeurs avant / après, et les actes de gestion (validations, exports, clôtures).",
            [
                new(null, "Détail", "Un clic sur une ligne affiche les valeurs avant et après la modification."),
            ]),
    ];

    /// <summary>Visite générique d'un écran sans guide : seuls les éléments communs.</summary>
    public static object VisiteGenerique() => new
    {
        Titre = "Cet écran",
        Etapes = new[] { new EtapeGuide("[data-guide=\"page-entete\"]", "", "Voici les éléments principaux de cet écran.", "bas") }
            .Concat(EtapesCommunes(null, null))
            .ToArray()
    };

    /// <summary>Guide de l'écran à cette adresse (route exacte d'abord, puis modèle à paramètre).</summary>
    public static GuideUtilisation? Pour(string chemin)
    {
        chemin = Normaliser(chemin);
        return Tous.FirstOrDefault(g => string.Equals(g.Route, chemin, StringComparison.OrdinalIgnoreCase))
            ?? Tous.FirstOrDefault(g => g.Route.Contains('{') && Correspond(g.Route, chemin));
    }

    /// <summary>
    /// Entrée de menu dont dépend l'accès au guide : celle de sa route, sinon celle dont
    /// la route est le plus long préfixe (une fiche suit les droits de sa liste).
    /// null pour un écran hors menu ouvert à tous (mot de passe).
    /// </summary>
    public static EntreeMenu? EntreeDe(string route) =>
        MenuApplication.ToutesLesEntrees()
            .Where(e => e.Href == route || (e.Href != "/" && route.StartsWith(e.Href + "/", StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(e => e.Href.Length)
            .FirstOrDefault();

    /// <summary>Section du menu à laquelle appartient le guide, pour le classement du manuel.</summary>
    public static SectionMenu? SectionDe(GuideUtilisation guide)
    {
        var entree = EntreeDe(guide.Route);
        return entree is null ? null : MenuApplication.Sections.FirstOrDefault(s => s.Entrees.Contains(entree));
    }

    private static string Normaliser(string chemin)
    {
        chemin = "/" + chemin.Split('?', '#')[0].Trim('/');
        return chemin;
    }

    private static bool Correspond(string modele, string chemin)
    {
        var attendus = modele.Trim('/').Split('/');
        var reels = chemin.Trim('/').Split('/');
        return attendus.Length == reels.Length
            && attendus.Zip(reels).All(p => p.First.StartsWith('{') || string.Equals(p.First, p.Second, StringComparison.OrdinalIgnoreCase));
    }
}
