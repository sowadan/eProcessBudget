# 01 — Démarrage

Objectif : lancer l'application, puis la brancher sur une base SQL Server.

---

## 1. Prérequis

| Outil | Version | Vérification |
|---|---|---|
| .NET SDK | 10.0 (LTS) | `dotnet --version` — version minimale imposée par `global.json` |
| SQL Server ou LocalDB | 2019+ | obligatoire |
| `dotnet-ef` | 10.x | `dotnet ef --version` — sinon `dotnet tool install --global dotnet-ef --version 10.*` |
| Node.js (facultatif) | 20+ | seulement pour régénérer la feuille Tailwind après avoir modifié l'interface : `npm install` dans `eProcessBudget.Web` (voir [07](07-composants-ui.md)) |

Sans Node.js, l'application fonctionne avec la feuille Tailwind déjà générée et versionnée.

---

## 2. Premier lancement (sans base)

```bash
dotnet run --project eProcessBudget.Web
```

L'application fonctionne uniquement sur SQL Server : renseignez d'abord la chaîne de
connexion (§ 3.1).

- l'application exige une connexion : fournissez d'abord le mot de passe du compte
  d'amorçage (Administrateur plateforme, identifiant `admin`) :

  ```bash
  cd eProcessBudget.Web
  dotnet user-secrets set "Amorcage:MotDePasse" "Un#MotDePasse2026"
  ```

- connectez-vous avec ce compte, créez un organisme (*Plateforme › Organismes*) puis son
  administrateur (*Administration › Utilisateurs*). Voir
  [04 — Plateforme et authentification](04-authentification.md).

---

## 3. La base SQL Server

### 3.1 La chaîne de connexion

`appsettings.json` pointe par défaut vers LocalDB :

```jsonc
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=eProcessBudget;Trusted_Connection=True;TrustServerCertificate=True"
}
```

Pour une autre instance, ne modifiez pas `appsettings.json` (fichier partagé) : déclarez la
chaîne du poste dans les secrets du projet Web, lus en développement par l'application
**et** par `dotnet ef` :

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=MON-POSTE;Database=GestionBudgetaire;Trusted_Connection=True;TrustServerCertificate=True" --project eProcessBudget.Web
```

### 3.2 Les migrations

Les migrations sont livrées dans `eProcessBudget.Infrastructure/Data/Migrations/`
(`SchemaInitial` : toutes les tables ; `VuesCalcul` : les vues de calcul). Pour créer la
base vous-même à partir d'un script :

```bash
cd eProcessBudget.Infrastructure
dotnet ef migrations script --idempotent --output eProcessBudget.sql
```

Voir [09 — Base de données](09-base-de-donnees.md) (dont la note sur `QUOTED_IDENTIFIER`).

### 3.3 Lancer

En `Development`, `MigrerAuDemarrage` vaut `true` : au lancement, l'application applique
les migrations puis le seed, qui crée :

- les profils du cahier (`ADMIN`, `ADMIN_PLATEFORME`, `DIRECTION`, `RESP_FINANCIER`…) ;
- une ligne de paramétrage par type de notification ;
- **si la table `Utilisateurs` est vide** et qu'`Amorcage:MotDePasse` est fourni, le
  compte d'amorçage, Administrateur plateforme.

La console affiche :

```
info: Demarrage[0] Compte d'amorçage admin prêt (Administrateur plateforme : True).
info: Demarrage[0] Base de données prête (migrations et données de référence appliquées).
```

Les autres utilisateurs se créent ensuite depuis *Administration › Utilisateurs*.

---

## 4. Poser votre identité

Section `Application` de `appsettings.json` :

```jsonc
"Application": {
  "Nom":         "eProcessBudget",       // titre de l'onglet, en-tête, emails
  "NomCourt":    "eProcessBudget",       // barre latérale (court !)
  "Sigle":       "EPB",                  // 1 à 3 lettres, logo par défaut
  "Description": "Gestion budgétaire",   // sous-titre de la barre latérale
  "LogoUrl":     "/logo.png",            // "" = pastille portant le sigle
  "Couleurs": {
    "Primaire":   "#C10A27",   // actions, accents, menu actif
    "Secondaire": "#01394F",   // barre latérale, titres
    "Tertiaire":  "#540421",   // dégradés
    "Accent":     "#FFB928"    // avatars, mises en avant
  }
}
```

Le nom, le sigle et les couleurs se propagent à l'interface, au thème MudBlazor et au
gabarit des emails.

---

## 5. Et ensuite

| Vous voulez… | Allez voir |
|---|---|
| comprendre où écrire votre code | [02 — Architecture](02-architecture.md) |
| créer votre première page métier | [08 — Ajouter une fonctionnalité](08-ajouter-une-fonctionnalite.md) |
| déclarer des droits | [05 — Droits et accès](05-droits-acces.md) |
| comprendre le schéma et les migrations | [09 — Base de données](09-base-de-donnees.md) |
| connaître les composants disponibles | [07 — Composants d'interface](07-composants-ui.md) |

---

## Problèmes courants au démarrage

| Symptôme | Cause | Solution |
|---|---|---|
| `ConnectionStrings:DefaultConnection est vide` | Chaîne absente, ou `Properties/launchSettings.json` manquant → `dotnet run` démarre en **Production** | Renseignez la chaîne (§ 3.1). À défaut de `launchSettings.json` : `$env:ASPNETCORE_ENVIRONMENT="Development"` avant `dotnet run`. |
| `Initialisation de la base impossible` | Serveur SQL injoignable, ou droits insuffisants | Testez la chaîne dans SSMS ; voir le diagnostic de [09](09-base-de-donnees.md). |
| `Compte d'amorçage « admin » sans mot de passe` (console) | `Amorcage:MotDePasse` absent | `dotnet user-secrets set "Amorcage:MotDePasse" "…"` puis relancez. |
| Connexion refusée avec le bon mot de passe | Compte bloqué après trop d'échecs (le message le dit), désactivé, ou organisme suspendu | Attendez la fin du blocage, ou faites débloquer le compte par un administrateur. |
| Une classe Tailwind n'a aucun effet | Classe absente de la feuille figée (nom composé par morceaux, ou feuille non régénérée) | Écrivez le nom en entier, puis `npm run css` — voir « Tailwind : une feuille figée » dans [07](07-composants-ui.md). |
| `Failed to determine the https port for redirect` | Aucun point d'écoute HTTPS configuré | Sans gravité en local ; `Program.cs` n'active la redirection que si un port HTTPS existe. |
