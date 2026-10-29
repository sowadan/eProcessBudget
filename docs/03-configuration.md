# 03 — Configuration

Toute la configuration du socle passe par `appsettings.json`. Chaque section est liée à
une classe typée du dossier `Infrastructure/Options/` : si vous cherchez ce que fait un
réglage, la classe correspondante le documente sur la propriété.

---

## Où mettre quoi

.NET empile les sources de configuration. La dernière lue l'emporte :

```
appsettings.json                       ← valeurs communes, VERSIONNÉ
        ▼
appsettings.{Environnement}.json       ← surcharges par environnement
        ▼
Variables d'environnement              ← secrets, surcharges serveur
        ▼
dotnet user-secrets                    ← secrets du poste développeur (hors dépôt)
```

| Type de valeur | Où la mettre | Pourquoi |
|---|---|---|
| Nom, sigle, couleurs | `appsettings.json` | Identique partout, sans secret. |
| Chaîne de connexion de dev | `appsettings.Development.json` ou `user-secrets` | Propre au poste. |
| Chaîne en sécurité intégrée, sans mot de passe | `appsettings.json` ou `appsettings.{Env}.json` | Acceptable : elle ne contient aucun secret, seulement un nom de serveur. |
| Chaîne de connexion de prod | Variable d'environnement | Ne doit jamais être versionnée. |
| Mot de passe SMTP | Variable d'environnement ou `user-secrets` | Idem. |

### Quel environnement est actif ?

La variable `ASPNETCORE_ENVIRONMENT` détermine quel `appsettings.{Environnement}.json`
est chargé. Sa valeur par défaut est **`Production`** — pas `Development`.

Sur le poste du développeur, c'est `eProcessBudget.Web/Properties/launchSettings.json` qui
la positionne à `Development`. **Ce fichier doit être versionné** : sans lui, `dotnet run`
démarre en Production, ignore votre `appsettings.Development.json`, et ne trouve plus
votre chaîne de connexion de développement.

```bash
dotnet run --project eProcessBudget.Web                          # profil "http",  port 5100
dotnet run --project eProcessBudget.Web --launch-profile https   # profil "https", port 7100
```

Pour vérifier ponctuellement le comportement de production en local, le profil
« Production (test local) » est fourni — il vous faudra alors une base de données.

Sur le serveur, `launchSettings.json` n'est **jamais déployé** : l'environnement s'y
règle par la variable d'environnement du système ou du pool d'applications.

**Convention des variables d'environnement :** les `:` deviennent `__` (double
souligné) :

```bash
ConnectionStrings__DefaultConnection="Server=...;Database=...;"
Email__Password="..."
```

Sur le poste de développement, préférez les secrets utilisateur :

```bash
cd eProcessBudget.Web
dotnet user-secrets init
dotnet user-secrets set "Email:Password" "motdepasse"
```

Ils sont stockés hors du dossier du projet : impossible de les committer par accident.

---

## `Application` — identité et charte

```jsonc
"Application": {
  "Nom":         "eProcessBudget",
  "NomCourt":    "eProcessBudget",
  "Sigle":       "EPB",
  "Description": "Gestion budgétaire",
  "LogoUrl":              "/img/logo-icone.svg",
  "LogoFondSombreUrl":    "/img/logo-icone-fond-sombre.svg",
  "AccentPremiereLettre": true,
  "Couleurs": {
    "Primaire":   "#2E75B6",
    "Secondaire": "#14254A",
    "Tertiaire":  "#1F3864",
    "Accent":     "#F2A33A"
  }
}
```

| Clé | Où cela apparaît |
|---|---|
| `Nom` | Titre de l'onglet, en-tête, sujet et gabarit des emails. |
| `NomCourt` | Barre latérale. Gardez-le court. |
| `Sigle` | 1 à 3 lettres. Pastille du logo quand `LogoUrl` est vide. |
| `Description` | Sous-titre du logotype, en petites capitales (barre latérale, connexion). |
| `LogoUrl` | Icône **carrée** pour fond clair, dans `wwwroot`. Vide = pastille au sigle. Préférez un SVG : net de 16 à 512 px. |
| `LogoFondSombreUrl` | Variante de l'icône pour fond sombre (barre latérale, page de connexion). Vide = `LogoUrl`. |
| `AccentPremiereLettre` | Met en couleur la première lettre du nom (le « e » bleu du logotype). |

Le nom à côté de l'icône est du texte (composant `AppMarque`), pas une image : il reste
net et lisible par les lecteurs d'écran. Les couleurs du logotype sont dans `app.css`
(classes `.marque-*`). Les icônes de navigateur (`favicon.ico`, `favicon.svg`,
`apple-touch-icon.png`) sont à la racine de `wwwroot` ; les logos horizontaux complets
(`img/logo-horizontal-fond-*.png`) sont réservés aux futurs exports et e-mails.

La charte livrée reprend les couleurs du logo : bleu de l'anneau (primaire), marine du
fond (secondaire, tertiaire), orange du point (accent).

### Comment les couleurs se propagent

Un seul endroit à modifier, trois consommateurs :

```
appsettings.json → Application:Couleurs
        │
        ├──► App.razor injecte  :root { --app-primaire: …; … }
        │         └──► app.css et tous les composants Tailwind
        │
        ├──► MainLayout.razor construit le MudTheme
        │         └──► tous les composants MudBlazor
        │
        └──► NotificationEmailFormatter
                  └──► gabarit HTML des emails
```

**N'écrivez jamais une couleur en dur** dans un composant. Utilisez
`var(--app-primaire)` en CSS, ou `Color.Primary` côté MudBlazor.

---

## `Persistance` — base de données

```jsonc
"Persistance": {
  "MigrerAuDemarrage": false               // true dans appsettings.Development.json
}
```

| Clé | Effet |
|---|---|
| `MigrerAuDemarrage` | Applique les migrations et insère les données de référence au lancement. `true` en dev, **`false` en production** (livré à `false` dans `appsettings.json` et `appsettings.Production.json`). |

> Attention : si la section est absente, la valeur par défaut du code est
> `MigrerAuDemarrage = true`. Gardez-la explicite.

Si la table `auth.Utilisateur` est vide, le seed crée le compte d'amorçage (section
`Amorcage`). Détails : [09 — Base de données](09-base-de-donnees.md).

---

## `ConnectionStrings`

```jsonc
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=eProcessBudget;Trusted_Connection=True;TrustServerCertificate=True"
}
```

| Clé | Rôle |
|---|---|
| `DefaultConnection` | Base SQL Server de l'application. **Obligatoire** : l'application refuse de démarrer si elle est vide. |

`TrustServerCertificate=True` accepte un certificat SQL auto-signé — fréquent en interne.
Si votre serveur a un certificat valide, retirez ce paramètre.

Le démarrage échoue avec un message explicite si le mode est activé mais la chaîne vide :
c'est volontaire, mieux vaut un arrêt net qu'une application à moitié fonctionnelle.

---

## `Securite` — connexion et sessions

```jsonc
"Securite": {
  "DureeSessionMinutes": 30,
  "NbTentativesMax": 5,
  "DureeBlocageMinutes": 15,
  "CoutHachage": 12,
  "DureeLienReinitialisationMinutes": 60,
  "IntervalleRevalidationSecondes": 60,
  "LimiteTentativesParMinute": 10,
  "MotDePasse": { "LongueurMin": 10, "LongueurMax": 128, "ExigerMajuscule": true,
                  "ExigerMinuscule": true, "ExigerChiffre": true, "ExigerCaractereSpecial": true }
}
```

| Clé | Effet |
|---|---|
| `DureeSessionMinutes`, `NbTentativesMax` | Valeurs **par défaut** : celles de l'organisme (`cfg.ParametreOrganisme`) l'emportent. Seules valeurs pour les comptes plateforme. |
| `DureeBlocageMinutes` | Durée du blocage après `NbTentativesMax` échecs consécutifs. |
| `CoutHachage` | Facteur de coût BCrypt ; jamais moins de 12, quelle que soit la valeur. Relevé, il s'applique à la connexion suivante de chaque compte. |
| `DureeLienReinitialisationMinutes` | Validité d'un lien « mot de passe oublié ». |
| `IntervalleRevalidationSecondes` | Fréquence de revérification d'une session ouverte (compte désactivé, organisme suspendu…). |
| `LimiteTentativesParMinute` | POST de connexion et de réinitialisation admis par minute et par adresse IP. |
| `MotDePasse` | Politique de complexité. |

Fonctionnement détaillé : [04 — Plateforme et authentification](04-authentification.md).

---

## `Amorcage` — le premier Administrateur plateforme

```jsonc
"Amorcage": {
  "Login": "admin",
  "NomPrenoms": "Administrateur plateforme",
  "Email": ""
}
```

Sur une base sans utilisateur, le seed crée ce compte, **à condition** que
`Amorcage:MotDePasse` soit fourni — jamais dans un fichier versionné :

| Environnement | Comment le fournir |
|---|---|
| Développement | `dotnet user-secrets set "Amorcage:MotDePasse" "…"` (dans `eProcessBudget.Web`) |
| Production | variable d'environnement `Amorcage__MotDePasse`, le temps du premier démarrage |

Le mot de passe doit respecter `Securite:MotDePasse`. Une fois le compte initialisé, la
section n'a plus d'effet : la configuration ne peut ni élever des droits ni changer le
mot de passe d'un compte en service.

---

## `Email` — relais SMTP des notifications

```jsonc
"Email": {
  "Active":                  false,
  "Host":                    "smtp.interne.local",
  "Port":                    25,
  "UseSsl":                  false,
  "User":                    "",
  "Password":                "",
  "ExpediteurApplicatif":    "notifications@exemple.local",
  "NomExpediteurApplicatif": "eProcessBudget — Notifications",
  "BaseUrl":                 "https://localhost:7100",
  "DelaiReessaiSecondes":    30,
  "NombreMaxTentatives":     3,
  "TimeoutSecondes":         30
}
```

| Clé | Détail |
|---|---|
| `Active` | Interrupteur global. À `false`, aucun email ne part — les notifications internes fonctionnent toujours. |
| `User` / `Password` | Laisser vide pour un relais interne anonyme. **Jamais de valeur réelle dans ce fichier.** |
| `ExpediteurApplicatif` | Adresse `From`. Reste toujours l'adresse applicative, même pour une notification déclenchée par un utilisateur (SPF/DMARC). |
| `BaseUrl` | Transforme les liens relatifs des notifications en liens absolus cliquables. **Vide = pas de bouton dans l'email.** |

Détails : [06 — Notifications](06-notifications.md).

---

## `DetailedErrors` — diagnostic temporaire

```jsonc
"DetailedErrors": true
```

Affiche la pile d'exception complète dans le navigateur (pages et circuits Blazor), y
compris hors développement. Utile pour diagnostiquer un premier déploiement, dangereux
ensuite : la pile révèle des chemins, des noms de tables et de serveurs.

| Fichier | Valeur attendue |
|---|---|
| `appsettings.Development.json` | `true` |
| `appsettings.Production.json` | **`false`** — ne le passer à `true` que le temps d'une analyse |

Le même principe vaut pour `web.config` : `stdoutLogEnabled="true"` et la variable
`ASPNETCORE_DETAILEDERRORS` servent à voir l'exception d'un démarrage en échec (erreur
500.30), puis se désactivent. Voir [10 — Déploiement](10-deploiement.md).

---

## `Logging`

```jsonc
"Logging": {
  "LogLevel": {
    "Default":                                       "Information",
    "eProcessBudget":                                "Information",
    "Microsoft.AspNetCore":                          "Warning",
    "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
  }
}
```

Passez `Microsoft.EntityFrameworkCore.Database.Command` à `Information` pour voir le SQL
généré — indispensable quand une requête est lente, à couper ensuite : c'est très verbeux.

En production, gardez `Default` à `Warning` et votre propre namespace à `Information` :
vous voyez ce que fait votre code sans le bruit du framework.

---

## Ajouter votre propre section

Quatre étapes, sur le modèle de celles du socle.

**1. La classe d'options** — `Infrastructure/Options/FacturationOptions.cs` :

```csharp
namespace eProcessBudget.Infrastructure.Options;

/// <summary>Paramètres du module de facturation, section "Facturation".</summary>
public class FacturationOptions
{
    public const string SectionName = "Facturation";

    /// <summary>Taux de TVA appliqué par défaut, en pourcentage.</summary>
    public decimal TauxTvaParDefaut { get; set; } = 18m;

    /// <summary>Nombre de jours avant échéance déclenchant une relance.</summary>
    public int JoursAvantRelance { get; set; } = 7;
}
```

**2. L'enregistrement** — dans `ServiceCollectionExtensions.AddSocleApplicatif` ou,
mieux, dans votre propre méthode d'extension :

```csharp
services.Configure<FacturationOptions>(
    configuration.GetSection(FacturationOptions.SectionName));
```

**3. La section** dans `appsettings.json` :

```jsonc
"Facturation": {
  "TauxTvaParDefaut": 18,
  "JoursAvantRelance": 7
}
```

**4. La consommation**, par injection :

```csharp
public class FactureService
{
    private readonly FacturationOptions _options;

    public FactureService(IOptions<FacturationOptions> options)
        => _options = options.Value;
}
```

Dans un composant Razor :

```razor
@inject IOptions<FacturationOptions> Facturation
<p>TVA : @Facturation.Value.TauxTvaParDefaut %</p>
```

> `IOptions<T>` lit la configuration une fois au démarrage. Pour une valeur rechargeable
> à chaud, utilisez `IOptionsMonitor<T>`. En pratique, `IOptions<T>` suffit presque
> toujours : un changement de configuration s'accompagne d'un redémarrage.

