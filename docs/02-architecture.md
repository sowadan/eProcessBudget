# 02 — Architecture

Ce document répond à une seule question, mais la plus fréquente : **où est-ce que j'écris
mon code ?**

---

## Vue d'ensemble

Trois projets, une dépendance unidirectionnelle. Une flèche signifie « référence » :

```
   eProcessBudget.Web  ─────►  eProcessBudget.Infrastructure  ─────►  eProcessBudget.Core
   (Blazor Server)          (EF Core, services)                 (entités, enums)

   ce que voit                ce qui accède aux                   ce dont on parle
   l'utilisateur              données et systèmes                 (le métier)
```

**La règle qui tient tout :** les flèches ne vont jamais dans l'autre sens.
`Core` ne connaît ni Entity Framework ni Blazor. C'est ce qui permet de changer de base
de données, ou d'ajouter une API REST à côté du Blazor, sans toucher au métier.

---

## eProcessBudget.Core — le domaine

Le vocabulaire de l'application. Aucune dépendance : ni EF Core, ni ASP.NET, rien.

```
Core/
├── Entities/
│   ├── BaseEntity.cs        Id + champs d'audit + suppression logique
│   ├── Utilisateur.cs       L'utilisateur applicatif
│   ├── Securite.cs          Profil, ProfilDroit, UtilisateurProfil, UtilisateurDroit,
│   │                        AuditLog
│   └── Notification.cs      Notification, ParametreNotification
└── Enums/
    ├── TypeDroit.cs         ★ Catalogue des droits — votre extension principale
    ├── TypeNotification.cs  ★ Catégories de notification
    └── DroitHelper.cs       Libellés et regroupements des droits
```

**Ce qui va ici :** vos entités métier, vos énumérations, les règles de calcul pur
(une méthode qui ne fait que transformer des données).

**Ce qui ne va pas ici :** tout ce qui lit ou écrit quelque part — base, fichier, réseau.

---

## eProcessBudget.Infrastructure — l'accès au monde extérieur

```
Infrastructure/
├── Data/
│   ├── AppDbContext.cs          Tables de l'application + conventions globales
│   └── SeedService.cs           Profils, paramètres, premier utilisateur (si MigrerAuDemarrage)
│
├── Migrations/                  Générées par EF Core (à créer : aucune n'est livrée)
│
├── Options/                     Une classe par section de appsettings.json
│   ├── ApplicationOptions.cs    Nom, sigle, couleurs
│   ├── PersistanceOptions.cs    Migrations au démarrage
│   ├── SecuriteOptions.cs       Sessions, blocage, politique de mot de passe
│   ├── AmorcageOptions.cs       Premier Administrateur plateforme
│   └── EmailOptions.cs
│
├── Services/
│   ├── Securite/
│   │   ├── AuthentificationService.cs    Connexion, blocage, réinitialisation, sessions
│   │   ├── UserContextService.cs         ★ Qui est l'utilisateur connecté (lu dans le cookie)
│   │   ├── IUtilisateurStore.cs          Lecture du référentiel utilisateurs
│   │   ├── IUtilisateurService.cs        Création, activation, attribution des profils
│   │   ├── IDroitService.cs              Que peut faire l'utilisateur
│   │   └── IProfilService.cs             Gestion des profils
│   ├── Audit/                            Journalisation des actions
│   ├── Notifications/                    Stockage, diffusion temps réel, email
│   └── Plateforme/                       Organismes (provisionnement), habilitations
│
└── ServiceCollectionExtensions.cs   ★ LE point de câblage unique
```

### Interfaces et implémentations

Chaque stockage du socle est une interface, implémentée sur SQL Server via EF Core et
enregistrée dans `ServiceCollectionExtensions` :

```
INotificationStore   →  NotificationStoreEf
IUtilisateurStore    →  UtilisateurStoreEf
IUtilisateurService  →  UtilisateurServiceEf
IDroitService        →  DroitServiceEf
IAuditService        →  AuditServiceEf
IProfilService       →  ProfilServiceEf
```

Le reste de l'application ne connaît **que l'interface** : les tests peuvent fournir
leurs propres implémentations sans toucher au code applicatif.

### Pourquoi `Features/` et pas `Pages/`

Chaque domaine fonctionnel a **son** dossier, avec tout ce qui le concerne — la liste,
le détail, les dialogues, les modèles de formulaire. Quand vous travaillez sur les
commandes, tout est sous les yeux :

```
Features/Commandes/
├── Index.razor              /commandes
├── Detail.razor             /commandes/{id}
├── DialogFormCommande.razor
└── CommandeFiltres.cs
```

Le routage Blazor vient de la directive `@page`, jamais de l'emplacement du fichier :
vous rangez comme vous voulez.

---

## Où écrire quoi — tableau de décision

| Vous voulez… | Fichier |
|---|---|
| une nouvelle entité métier | `Core/Entities/` + `DbSet` dans `AppDbContext` |
| un nouveau droit | `Core/Enums/TypeDroit.cs` + libellé dans `DroitHelper.cs` |
| un nouveau type de notification | `Core/Enums/TypeNotification.cs` + `NotificationHelpers.cs` |
| une nouvelle page | `Web/Features/<Domaine>/Index.razor` |
| l'entrée de menu correspondante | `Web/Components/Layout/MenuApplication.cs` |
| un service métier | `Infrastructure/Services/<Domaine>/` + enregistrement |
| un composant réutilisable | `Web/Components/UI/` |
| un réglage configurable | classe dans `Infrastructure/Options/` + section dans `appsettings.json` |
| changer les couleurs | `appsettings.json` → `Application:Couleurs` **uniquement** |
| enregistrer un service | `Infrastructure/ServiceCollectionExtensions.cs` |

---

## Durées de vie des services

Blazor Server maintient un **circuit** par onglet ouvert. Choisir la mauvaise durée de
vie provoque des bugs difficiles à diagnostiquer — un utilisateur qui voit les données
d'un autre, par exemple.

| Durée de vie | Quand l'utiliser | Exemples du socle |
|---|---|---|
| **Singleton** | état partagé par tout le serveur | `NotificationDispatcher`, `EmailQueue` |
| **Scoped** | un par circuit Blazor (par onglet) | `AuthService`, `UserContextService`, services EF Core |
| **Transient** | sans état, instancié à chaque demande | rarement nécessaire ici |

**Règle** : tout ce qui touche à l'utilisateur courant est **Scoped**.
`UserContextService` met en cache l'utilisateur pour la durée du circuit — en Singleton,
il servirait le même utilisateur à tout le monde.

---

## Pourquoi `IDbContextFactory` et pas `AddDbContext`

Le socle enregistre `AddDbContextFactory<AppDbContext>` et crée un contexte par
opération :

```csharp
await using var ctx = await _factory.CreateDbContextAsync();
var lignes = await ctx.Budgets.ToListAsync();
```

En Blazor Server, plusieurs composants d'une même page peuvent charger leurs données
**en parallèle**. Un `DbContext` partagé entre eux lève
*« A second operation was started on this context instance before a previous operation
completed »*. La factory évite le problème par construction.

---

## Le chemin d'une requête

Ce qui se passe quand un utilisateur ouvre `/administration/utilisateurs` :

```
1. Navigateur              GET /administration/utilisateurs
2. Program.cs              Pipeline : fichiers statiques → Antiforgery
3. Routes.razor            Résout la page et applique MainLayout
4. MainLayout.razor        Construit le menu filtré par les droits (IDroitService),
                           pour l'utilisateur donné par IUserContextService
5. Utilisateurs.razor      <AutoriseSi> vérifie le droit de la page
6. IUtilisateurStore       Charge les données, filtrage et pagination côté service
7. Rendu                   HTML envoyé, puis circuit SignalR établi pour l'interactivité
```

---

## Sécurité : les trois niveaux

| Niveau | Mécanisme | Ce qu'il protège |
|---|---|---|
| **Identité** | Cookie de session + `IUserContextService` | Qui est l'utilisateur connecté, et son organisme. Toute page exige une session, sauf celles du compte. Voir [04](04-authentification.md). |
| **Affichage** | `<AutoriseSi>` | Ce que l'utilisateur voit : pages, boutons, colonnes. |
| **Action** | `IDroitService` dans le service | Ce que l'utilisateur peut réellement faire. |

**Le niveau 3 n'est pas optionnel.** Masquer un bouton n'empêche personne d'appeler le
service autrement. Détails dans [05 — Droits et accès](05-droits-acces.md).

---

## Conventions de code

| Point | Convention | Exemple |
|---|---|---|
| Langue | français pour le métier, anglais pour la technique | `ObtenirDroitsAsync`, `IDbContextFactory` |
| Champs privés | `_` en préfixe, `camelCase` | `_chargement`, `_userId` |
| Async | suffixe `Async`, `await` systématique | `ChargerAsync()` |
| Erreur métier | tuple de retour, pas d'exception | `Task<(bool Ok, string? Erreur)>` |
| Erreur technique | exception | chaîne de connexion invalide |
| Entités | suppression logique via `EstSupprime` | filtre global dans `AppDbContext` |
| Dates | **UTC en stockage**, local à l'affichage | `DateTime.UtcNow` puis `.ToLocalTime()` |

La distinction erreur métier / erreur technique compte : « la référence existe déjà »
est un cas prévu qu'on affiche à l'utilisateur, pas un incident qu'on journalise en
erreur et qui remonte jusqu'à la page d'exception.

