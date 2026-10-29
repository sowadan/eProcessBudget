# eProcessBudget

Application Blazor Server de gestion budgétaire, construite sur un socle en couches
(Core / Infrastructure / Web).

Le socle fournit : **authentification par cookie**, **organismes cloisonnés**
(multi-organisme), **utilisateurs**, **droits regroupés en profils**, **habilitations à
double validation**, **notifications temps réel**, **journal d'audit** et une
**bibliothèque de composants d'interface**.

---

## Démarrage

```bash
dotnet run --project eProcessBudget.Web
```

Ouvrez `http://localhost:5100`. L'application fonctionne uniquement sur SQL Server
(`ConnectionStrings:DefaultConnection`) ; en développement, elle applique les migrations
et crée le compte d'amorçage `admin` au premier lancement (voir « Connexion »).
Voir [docs/09-base-de-donnees.md](docs/09-base-de-donnees.md).

---

## Connexion

L'application exige une connexion (identifiant et mot de passe, cookie de session). Au
premier démarrage sur une base vide, le seed crée le compte d'amorçage `admin`,
**Administrateur plateforme**, si son mot de passe est fourni :

```bash
cd eProcessBudget.Web
dotnet user-secrets set "Amorcage:MotDePasse" "Un#MotDePasse2026"
```

Avec ce compte : *Plateforme › Organismes* pour créer un organisme, puis
*Administration › Utilisateurs* pour créer son administrateur. Tout le fonctionnement
(blocage, sessions, habilitations à double validation) : [docs/04](docs/04-authentification.md).

---

## Ce que contient le socle

| Fonctionnalité | Détail |
|---|---|
| **Authentification** | Connexion BCrypt, blocage après N échecs, sessions à expiration glissante, mot de passe oublié, journal des connexions. |
| **Organismes** | Création avec données initiales, suspension (*Plateforme › Organismes*). |
| **Utilisateurs** | Création, modification, activation, déblocage, attribution de profils (*Administration › Utilisateurs*). |
| **Habilitations** | Droits par périmètre, actifs après double validation (*Administration › Habilitations*). |
| **Paramétrage** | Organisme, exercices et cycles, nomenclature budgétaire, 20 référentiels, import / export Excel (*Paramétrage*). |
| **Budget-programme** | Programmes, actions, activités, indicateurs et cibles, cadre logique, programmation triennale AE / CP, ressources et emplois, répartitions, performance, analyses (*Budget-programme*). |
| **Exécution budgétaire** | Cycle commun aux axes de dépenses : prévisions, ajustements motivés, engagements AE / AD, dépassements soumis au Responsable Budget, factures, paiements, alertes de seuil, clôture avec reports (*Exécution budgétaire*). |
| **Droits et profils** | Droits regroupés en profils réutilisables, plus des droits directs pour les exceptions. |
| **Notifications** | Cloche avec badge, push temps réel, page d'historique, canal email optionnel, paramétrage par type d'événement. |
| **Journal d'audit** | Qui a fait quoi, quand, avec les états avant/après. |
| **Composants d'interface** | Boutons, cartes, badges, alertes, tuiles d'indicateur, états vides, dialogues, icônes. |
| **Charte configurable** | Nom, sigle, logo et quatre couleurs dans `appsettings.json`. |
| **Base SQL Server** | EF Core 10, migrations versionnées, cloisonnement par organisme. |

---

## Documentation

| # | Document | Ce que vous y trouvez |
|---|---|---|
| 01 | [Démarrage](docs/01-demarrage.md) | Prérequis, premier lancement, passage en base. |
| 02 | [Architecture](docs/02-architecture.md) | Les trois couches, le rôle de chaque dossier, **où écrire quoi**. |
| 03 | [Configuration](docs/03-configuration.md) | Chaque réglage de `appsettings.json`. |
| 04 | [Plateforme et authentification](docs/04-authentification.md) | Connexion, sessions, organismes, habilitations. |
| 05 | [Droits et accès](docs/05-droits-acces.md) | Ajouter un droit, protéger une page. |
| 06 | [Notifications](docs/06-notifications.md) | Envoyer une notification, activer l'email. |
| 07 | [Composants d'interface](docs/07-composants-ui.md) | Catalogue des composants, feuille Tailwind figée. |
| 08 | [Ajouter une fonctionnalité](docs/08-ajouter-une-fonctionnalite.md) | Le parcours complet, de l'entité à l'entrée de menu. |
| 09 | [Base de données](docs/09-base-de-donnees.md) | Tables, migrations, seed. |
| 10 | [Déploiement](docs/10-deploiement.md) | Liste de contrôle et publication IIS. |
| 11 | [Paramétrage](docs/11-parametrage.md) | Référentiels, exercices, nomenclature, import Excel, ajouter un référentiel. |
| 12 | [Budget-programme](docs/12-budget-programme.md) | Programmes, indicateurs, cadre logique, programmation AE / CP, répartitions, périmètre de saisie. |
| 13 | [Exécution budgétaire](docs/13-execution.md) | Dotations, ajustements, engagements, dépassements, paiements, alertes, clôture. |
| 14 | [Axes de dépenses](docs/14-axes-depenses.md) | Entretien routier, Investissements, Fonctionnement, Appuis : projets, contrats, avancement, inventaire, synthèses. |
| 15 | [Recettes](docs/15-recettes.md) | Prévisions, encaissements mensuels, échéancier de recouvrement, reports de ressources, bilan recettes / dépenses. |
| 16 | [Consommation et tableau de bord](docs/16-consommation-tableau-de-bord.md) | Vision consolidée tous axes, projection, dérapages, évolution, tableau croisé ; tableau de bord d'accueil. |
| 17 | [Rapports et exportation](docs/17-rapports.md) | 18 états de gestion et du budget-programme, Excel (ClosedXML) et PDF (QuestPDF), impression, licence QuestPDF à vérifier. |
| 18 | [Audit et traçabilité](docs/18-audit.md) | Journalisation automatique des écritures (avant / après), ajout seul, adresse IP, purge selon la rétention, écran filtré et export. |
| 19 | [Réponses du métier](docs/19-reponses-metier.md) | Annexe C.2 : indicateurs de l'entretien, réhabilitation en investissement, facture obligatoire, factures émises, comptes sur chiffres, équilibre de trésorerie, reprise de l'historique. |
| 20 | [Guide d'utilisation](docs/20-guide-utilisation.md) | Manuel dans l'application, filtré par droits ; visites guidées des écrans (driver.js) ; documenter un écran. |

---

## Structure de la solution

```
eProcessBudget/
├── eProcessBudget.sln
│
├── eProcessBudget.Core/              ← Domaine pur : entités et énumérations
│   ├── Entities/                     Utilisateur, Profil, droits, notifications, audit
│   └── Enums/                        TypeDroit et TypeNotification : vos points d'extension
│
├── eProcessBudget.Infrastructure/    ← Accès données et services techniques
│   ├── Data/                         AppDbContext, données de référence (seed)
│   ├── Options/                      Classes typées liées à appsettings.json
│   ├── Services/
│   │   ├── Securite/                 Authentification, utilisateur connecté, profils, droits
│   │   ├── Plateforme/               Organismes, habilitations
│   │   ├── Audit/
│   │   └── Notifications/
│   └── ServiceCollectionExtensions.cs   ← LE point de câblage unique
│
├── eProcessBudget.Web/               ← Application Blazor Server
│   ├── Components/
│   │   ├── Layout/                   Barre latérale, en-tête, cloche, MENU
│   │   ├── Shared/                   Logo, icônes
│   │   └── UI/                       Composants réutilisables (AppButton, AppCard, AutoriseSi…)
│   ├── Features/
│   │   ├── Accueil/                  Tableau de bord
│   │   ├── Compte/                   Connexion, mot de passe oublié, mon mot de passe
│   │   ├── Plateforme/               Organismes
│   │   ├── Notifications/            Historique
│   │   ├── Administration/           Utilisateurs, profils, canaux, audit
│   │   └── Systeme/                  Page d'erreur
│   ├── Securite/                 Cookie, autorisation par défaut, déconnexion
│   ├── wwwroot/
│   ├── appsettings*.json
│   └── Program.cs
│
├── eProcessBudget.Tests/             ← xUnit : authentification, habilitations, cloisonnement
│
└── docs/
```

Les migrations sont livrées dans `eProcessBudget.Infrastructure/Data/Migrations/`
(voir [docs/09-base-de-donnees.md](docs/09-base-de-donnees.md)).

---

## Les quatre fichiers à connaître

| Fichier | Rôle |
|---|---|
| `eProcessBudget.Core/Enums/TypeDroit.cs` | Déclarer un nouveau droit. |
| `eProcessBudget.Web/Components/Layout/MenuApplication.cs` | Ajouter l'entrée de menu et sa condition d'affichage. |
| `eProcessBudget.Web/Features/<Domaine>/` | La page elle-même. |
| `eProcessBudget.Infrastructure/ServiceCollectionExtensions.cs` | Enregistrer un nouveau service. |

---

## Prérequis

| Outil | Version |
|---|---|
| .NET SDK | 10.0 (LTS) — fixé par `global.json` |
| SQL Server 2019+ (ou LocalDB) | obligatoire |
| `dotnet-ef` | 10.x, pour créer les migrations |

MudBlazor (9.x, compatible .NET 10) est sous licence MIT.

Les versions des packages NuGet sont centralisées dans `Directory.Packages.props` et le
framework cible (`net10.0`) dans `Directory.Build.props` : c'est là, et seulement là,
qu'on fait une montée de version.
