# 09 — Base de données

Mettre en place la base de l'application, gérer les migrations, ajouter une entité.

L'application fonctionne uniquement sur SQL Server. La base porte le socle
(utilisateurs, profils, droits, notifications, audit) et toutes les tables des modules
métier décrites au chapitre 3 du cahier technique
(`cdc/e-Process-Budget_Cahier_Technique_v1.1.docx`).

---

## Mettre en place la base, en 4 étapes

### 1. Configurer

La chaîne livrée vise LocalDB, qui crée la base au premier `database update` :

```jsonc
{
  "Persistance": {
    "MigrerAuDemarrage": true
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=eProcessBudget;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Sur un SQL Server partagé, le compte applicatif a besoin de `db_datareader`,
`db_datawriter` et `db_ddladmin` (ce dernier uniquement si les migrations sont
appliquées par l'application).

### 2. Les migrations livrées

Trois migrations, dans `eProcessBudget.Infrastructure/Data/Migrations/` :

| Migration | Contenu |
|---|---|
| `SchemaInitial` | Les 75 tables (73 du cahier technique + `auth.UtilisateurDroit` et `ntf.ParametreNotification` du socle), clés, index, contraintes. |
| `VuesCalcul` | Les 4 vues de calcul du § 3.5 (`bud.vw_SituationDotation`, `prog.vw_ConsommationActivite`, `prog.vw_AtteinteIndicateur`, `rec.vw_SituationRecette`). Leur SQL est écrit dans le fichier de migration. |
| `StatutActifReferentiels` | Colonne `EstActif` (défaut 1 : l'existant reste actif) sur 9 référentiels que le dictionnaire n'en dotait pas. Voir les écarts ci-dessous. |
| `StatutActifIndicateur` | Colonne `EstActif` (défaut 1) sur `prog.Indicateur`, comme sur les programmes, actions et activités. |

Pour créer la base à partir d'un script (relu par l'exploitant) :

```bash
cd eProcessBudget.Infrastructure
dotnet ef migrations script --idempotent --output eProcessBudget.sql
```

> **`QUOTED_IDENTIFIER` doit être à `ON`** pour exécuter ce script : les index uniques
> sont filtrés (`WHERE [EstSupprime] = 0`). SSMS et l'application le sont par défaut ;
> avec `sqlcmd`, ajoutez l'option `-I`.

Vos évolutions suivantes s'ajoutent par de **nouvelles** migrations — ne modifiez jamais
une migration déjà appliquée sur une base partagée.

Les commandes `dotnet ef` se lancent depuis `eProcessBudget.Infrastructure` (ou depuis la
solution avec `--project eProcessBudget.Infrastructure`) :
`AppDbContextDesignFactory` (Infrastructure/Data) fournit le contexte aux outils. Par
défaut, `dotnet ef database update` vise **la même base que l'application** :
`ConnectionStrings:DefaultConnection` de `eProcessBudget.Web/appsettings.json`, surchargée
par `appsettings.Development.json`. Pour viser une autre base : option `--connection "..."`
ou variable d'environnement `EPB_CONNEXION_DESIGN`. Vérifiez la cible avant d'appliquer :
`dotnet ef dbcontext info` affiche le serveur et la base.

Si `dotnet ef` n'est pas installé :

```bash
dotnet tool install --global dotnet-ef --version 10.*
```


### 3. Lancer

```bash
dotnet run --project eProcessBudget.Web
```

`MigrerAuDemarrage: true` applique la migration et insère les données de référence.
La console affiche :

```
info: Demarrage[0] Premier utilisateur admin créé avec le profil ADMIN.
info: Demarrage[0] Base de données prête (migrations et données de référence appliquées).
```

### 4. Le premier utilisateur

Si la table `auth.Utilisateur` est vide, le seed crée le **compte d'amorçage** décrit par
la section `Amorcage`, Administrateur plateforme, à condition qu'`Amorcage:MotDePasse`
soit fourni (user-secrets ou variable d'environnement). Avec ce compte, on crée les
organismes puis leurs administrateurs. Voir [04 — Plateforme et authentification](04-authentification.md).

Une base créée avant le module Authentification contient déjà un compte `admin` sans mot
de passe : le seed lui donne le mot de passe fourni (une seule fois) et, s'il n'a pas
d'organisme, en fait l'Administrateur plateforme. Un compte qui a déjà un mot de passe
n'est jamais modifié par la configuration.

> **En production.** Le seed ne s'exécute que si `MigrerAuDemarrage = true`. Avec la
> valeur de production (`false`), une base neuve n'a ni profil ni utilisateur. Pour le
> premier déploiement, démarrez l'application une fois avec la variable d'environnement
> `Persistance__MigrerAuDemarrage=true` (le compte SQL doit alors avoir `db_ddladmin`),
> vérifiez les journaux, puis retirez la variable. Voir [10 — Déploiement](10-deploiement.md).

---

## Ce que crée le seed

`SeedService` s'exécute à chaque démarrage **où `MigrerAuDemarrage = true`** et est
**idempotent** : il ne duplique rien. En production (`false`), il ne tourne pas : un
droit ajouté à `TypeDroit` n'est donc **pas** accordé automatiquement au profil `ADMIN` —
accordez-le depuis l'écran *Profils*, ou relancez une fois avec
`Persistance__MigrerAuDemarrage=true`.

| Élément | Détail |
|---|---|
| Profil `ADMIN` | Tous les droits d'organisme (`TypeDroit` sauf `DroitHelper.ReservesPlateforme`). Rejoué à chaque démarrage : un droit ajouté à l'énumération lui est accordé automatiquement. |
| Profil `ADMIN_PLATEFORME` | `DroitHelper.AdmisPlateforme`, rejoué de même. |
| Autres profils du cahier | `DIRECTION`, `RESP_FINANCIER`, `RESP_BUDGET`, `RESP_PROGRAMME`, `COMPTABLE`, `AUDITEUR`, `CONSULTATION` : créés s'ils manquent, jamais modifiés ensuite. |
| `ParametresNotification` | Une ligne par valeur de `TypeNotification` — interne activé, email désactivé. |
| Compte d'amorçage | Créé depuis `Amorcage` avec le profil `ADMIN_PLATEFORME`, **uniquement** si la table `auth.Utilisateur` est vide et qu'un mot de passe est fourni. |

Le seed **ajoute** les droits manquants mais n'en retire jamais. Pour restreindre le
profil `ADMIN` (par exemple l'exclure d'un droit métier sensible), filtrez la liste dans
`SeedService.InitialiserProfilsAsync` :

```csharp
var tousLesDroits = Enum.GetValues<TypeDroit>()
                        .Where(d => d != TypeDroit.MonDroitSensible)
                        .ToList();
```

> Un droit déjà accordé reste en base : retirez-le depuis l'écran *Profils* ou par SQL.

---

## Organisation du schéma

Un schéma SQL par domaine (cahier technique § 3.1). Un fichier par module, qui contient
toutes ses classes dans l'ordre du cahier (comme pour les énumérations) :

| Module | Entités (`eProcessBudget.Core/Entities/`) | Configurations (`eProcessBudget.Infrastructure/Data/Configurations/`) | Énumérations (`eProcessBudget.Core/Enums/`) |
|---|---|---|---|
| Plateforme et habilitations | `Plateforme.cs` | `ConfigurationsPlateforme.cs` | `EnumsPlateforme.cs` |
| Paramètres généraux | `Parametrage.cs` | `ConfigurationsParametrage.cs` | `EnumsParametrage.cs` |
| Référentiels | `Referentiels.cs` | `ConfigurationsReferentiels.cs` | `EnumsParametrage.cs` |
| Budget-programme | `BudgetProgramme.cs` | `ConfigurationsBudgetProgramme.cs` | `EnumsBudgetProgramme.cs` |
| Cycle budgétaire et exécution | `Execution.cs` | `ConfigurationsExecution.cs` | `EnumsExecution.cs` |
| Recettes | `Recettes.cs` | `ConfigurationsRecettes.cs` | `EnumsExecution.cs` |
| Historique | `Historique.cs` | `ConfigurationsHistorique.cs` | `EnumsExecution.cs` |
| Vues de calcul | `Vues.cs` | `ConfigurationsVues.cs` | - |

Les entités du socle restent dans leurs fichiers d'origine (`Utilisateur.cs`,
`Securite.cs`, `Notification.cs`, `BaseEntity.cs`).

| Schéma | Domaine | Tables |
|---|---|---|
| `plat` | Plateforme | `Organisme` |
| `auth` | Authentification et habilitations | `Utilisateur`, `Profil`, `ProfilDroit`, `UtilisateurProfil`, `UtilisateurDroit`, `Habilitation`, `JournalConnexion` |
| `cfg` | Paramètres généraux | `ParametreOrganisme`, `Fichier`, `Devise`, `TauxChange`, `CycleProgrammation`, `Exercice`, `Periode`, `SeuilAlerte` |
| `ref` | Référentiels | `ListeValeur`, `Axe`, `Structure`, `Site`, `SiteTiers`, `CategorieEmploi`, `Agent`, `NatureDepense`, `SourceFinancement`, `CompteComptable`, `Rubrique`, `SousRubrique`, `LigneBudgetaire`, `LigneCompte`, `MotifAjustement`, `UniteMesure`, `Risque`, `TypeTravaux`, `Tiers`, `NatureRecette`, `CompteBancaire` |
| `prog` | Budget-programme | `Programme`, `ProgrammeStructure`, `Action`, `Activite`, `CleRepartition`, `Indicateur`, `IndicateurCible`, `IndicateurRealisation`, `CadreLogiqueElement`, `CadreLogiqueRisque`, `ProgrammationFinanciere`, `ProgrammationRessource`, `ProgrammationEmploi`, `RepartitionTemps`, `AnalyseQualitative` |
| `bud` | Cycle budgétaire et exécution | `Projet`, `ProjetTravaux`, `Contrat`, `Dotation`, `Ajustement`, `Report`, `Engagement`, `EcheancierCP`, `ValidationDepassement`, `Facture`, `Paiement`, `RejetPenalite`, `AvancementPhysique`, `Immobilisation`, `EnveloppeMaitreOuvrage` |
| `rec` | Recettes | `PrevisionRecette`, `Encaissement`, `EcheanceRecouvrement`, `ReportRessource` |
| `hist` | Historique | `ExecutionHistorique` |
| `ntf` | Alertes | `Notification`, `ParametreNotification` |
| `aud` | Audit | `JournalAudit` (entité `AuditLog`) |

La table `prog.Action` est portée par la classe `ActionProgramme`, pour ne pas masquer le
délégué `System.Action`.

### Écarts assumés avec le cahier technique

| Point | Choix |
|---|---|
| Colonnes standard | Noms français du socle : `DateCreation`, `CreePar`, `DateModification`, `ModifiePar`, `EstSupprime`, `DateSuppression`, `SupprimePar`, `RowVersion` (et non `CreatedAt`, `IsDeleted`...), conformément au § 2.5. |
| `auth.Utilisateur` | `Matricule` tient lieu de `Login`, `NomPrenoms` de `Nom` + `Prenoms` (champs du socle conservés). |
| `ntf.Notification` | `CleEntite` (« Entite:Id ») tient lieu de `EntiteNom` + `EntiteId`. |
| `aud.JournalAudit` | `Action` reste en texte (socle) ; l'énumération `ActionAudit` en fournit les valeurs. |
| Tables du socle | Gardent la suppression logique et les champs d'audit complets, y compris `ProfilDroit` et `UtilisateurProfil` (allégées dans le cahier). |
| Statut actif / inactif | `EstActif` ajouté sur `Axe`, `CategorieEmploi`, `NatureDepense`, `SourceFinancement`, `MotifAjustement`, `UniteMesure`, `Risque`, `TypeTravaux`, `NatureRecette` : le § 3.2.1 du cahier fonctionnel l'exige pour chaque référentiel, le dictionnaire l'omettait (migration `StatutActifReferentiels`). ; de même sur `Indicateur` (migration `StatutActifIndicateur`). |

---

## Conventions appliquées automatiquement

`AppDbContext` impose ces règles à **toutes** les entités, y compris les vôtres (fin de
`OnModelCreating`). Une configuration d'entité ne déclare donc que ce qui est propre à
sa table : nom et schéma, longueurs, précisions, index et contraintes métier.

### Identifiants ULID

Toutes les clés (primaires, étrangères, champs d'audit `CreePar`, `ModifiePar`...) sont
de type `Ulid` (package NuGet `Ulid`, type `System.Ulid`), jamais `int`.

- **Génération côté application** : à l'`Add`, toute entité dont l'`Id` vaut
  `Ulid.Empty` reçoit un nouvel ULID (`UlidValueGenerator`). La valeur est définitive
  immédiatement : utilisable comme clé étrangère avant même le `SaveChanges`.
- **Stockage** : `binary(16)` (cahier technique § 2.4). Les 48 premiers bits sont un
  horodatage big-endian : le tri binaire suit l'ordre de création et l'index cluster
  reste compact. Dans l'interface et les URL, l'ULID s'affiche sur 26 caractères
  (`ToString()` / `Ulid.TryParse`).
- **« Pas d'identifiant »** s'écrit `Ulid.Empty` (là où l'on utilisait `0`) ; c'est
  notamment ce que renvoie `ObtenirUserIdAsync()` pour un utilisateur inconnu.

Dans vos entités :

```csharp
public Ulid DemandeurId { get; set; }
public Ulid? ValideurId { get; set; }
```

Aucune configuration à ajouter : la conversion est déclarée une fois pour tout le modèle
dans `AppDbContext.ConfigureConventions`. Dans une URL, utilisez un paramètre `string` puis
`Ulid.TryParse` (Blazor ne propose pas de contrainte de route `:ulid`).

### Classes de base

| Classe | Tables | Colonnes |
|---|---|---|
| `BaseEntity` | socle, `plat.Organisme` | `Id` + audit + suppression logique + `RowVersion` |
| `EntiteOrganisme` | presque toutes les tables métier | `BaseEntity` + `OrganismeId` |
| `EntiteLiaison` | tables allégées (`SiteTiers`, `LigneCompte`, `ProgrammeStructure`, `CadreLogiqueRisque`) | `Id`, `OrganismeId`, `DateCreation`, `CreePar` |

### Multi-organisme

Toute entité `IEntiteOrganisme` reçoit automatiquement une clé étrangère vers
`plat.Organisme` et un filtre global sur l'organisme courant, porté par le contexte :

```csharp
await using var ctx = await factory.CreateDbContextAsync();
ctx.OrganismeCourantId = organismeId;          // obligatoire pour les tables métier
var axes = await ctx.Axes.ToListAsync();       // uniquement les axes de cet organisme
```

- **Sans `OrganismeCourantId`, une requête métier ne renvoie rien** : un oubli ne peut
  pas exposer les données d'un autre organisme.
- À l'enregistrement, `OrganismeId` est renseigné sur les créations ; toute création,
  modification ou suppression visant un autre organisme lève une exception.
- `ctx.ContournerFiltreOrganisme = true` désactive ces deux protections : réservé au
  provisionnement d'un organisme (module Plateforme) et aux traitements planifiés.

> **Dans les services, c'est automatique.** `IDbContextFactory<AppDbContext>` est
> `FabriqueContexteOrganisme` : chaque contexte créé par `CreateDbContextAsync` reçoit
> l'organisme de l'utilisateur connecté (lu dans le cookie). Renseigner
> `OrganismeCourantId` à la main ne sert qu'aux traitements planifiés, qui utilisent la
> fabrique sans organisme (service à clé `FabriqueContexteOrganisme.CleSansOrganisme`).

### Autres règles automatiques

| Règle | Effet |
|---|---|
| Concurrence | `RowVersion` (rowversion) sur toutes les tables complètes : un conflit lève `DbUpdateConcurrencyException`. |
| Clés étrangères | Toutes en `NO ACTION` : aucune suppression en cascade. |
| Énumérations | Stockées en `tinyint` avec une contrainte `CK_Table_Colonne` limitée aux valeurs de l'énumération. Ajouter une valeur produit une migration. |
| Index uniques | Nommés `UX_Table_Colonnes`, filtrés sur `[EstSupprime] = 0` pour les tables métier : un code supprimé peut être réutilisé. |
| Types | `decimal` en `(18,2)` par défaut (pourcentages `(7,4)` et valeurs d'indicateurs `(19,4)` déclarés dans les configurations) ; `DateTime` en `datetime2(3)` UTC ; dates métier en `DateOnly` (`date`). |
| Valeurs par défaut | `DateCreation` = `SYSUTCDATETIME()`, contrainte nommée `DF_Table_Colonne`. |

### Suppression logique

Toute entité héritant de `BaseEntity` reçoit un filtre global : les lignes
`EstSupprime = true` sont invisibles dans toutes les requêtes.

```csharp
// Supprimer
entite.EstSupprime = true;
entite.DateSuppression = DateTime.UtcNow;
entite.SupprimePar = userId;
await ctx.SaveChangesAsync();

// Les inclure exceptionnellement (écran de restauration, audit)
// Attention : IgnoreQueryFilters lève AUSSI le filtre d'organisme.
var tout = await ctx.Axes.IgnoreQueryFilters()
                         .Where(a => a.OrganismeId == ctx.OrganismeCourantId)
                         .ToListAsync();
```

> **Attention aux jointures.** Le filtre s'applique aussi aux `Include` : une commande
> dont le demandeur est supprimé verra `Demandeur` à `null`. Prévoyez-le.

### Longueur des chaînes bornée

Toute propriété `string` sans `HasMaxLength` explicite est bornée à **400 caractères**
(un texte long se déclare avec `.EnTexteLong()` dans sa configuration).
Sans cela, EF Core génère du `nvarchar(max)` : coûteux en stockage, et **non indexable**.

Déclarez toujours la longueur qui a du sens :

```csharp
e.Property(x => x.Reference).HasMaxLength(30);
e.Property(x => x.Commentaire).HasMaxLength(2000);
```

### Horodatage automatique

`SaveChanges` renseigne `DateModification` sur toute entité modifiée : la traçabilité ne
dépend pas de la vigilance de l'appelant.

### Vues de calcul

Les agrégats budgétaires (prévision ajustée, dotation totale, AE, paiements, restes à
payer, taux sur deux bases) sont calculés par des vues SQL, lues comme des entités sans
clé : `ctx.SituationsDotations`, `ctx.ConsommationsActivites`, `ctx.AtteintesIndicateurs`,
`ctx.SituationsRecettes`. Elles reçoivent le même filtre d'organisme que les tables.
Seules les pièces au statut *Validé* y sont comptées.

---

## Les migrations

### Créer

```bash
cd eProcessBudget.Infrastructure
dotnet ef migrations add AjoutBudgets --output-dir Data/Migrations
```

Nommez par **ce que fait** la migration (`AjoutBudgets`, `IndexSurReference`), pas par
une date.

### Relire — étape non optionnelle

Ouvrez le fichier généré dans `Migrations/`. Cherchez :

| Signal | Risque |
|---|---|
| `DropColumn` | **Perte de données définitive.** |
| `DropTable` | Idem. |
| `AlterColumn` réduisant une longueur | Troncature ou échec sur les données existantes. |
| `AddColumn` non nullable sans `defaultValue` | Échec si la table contient des lignes. |
| Renommage vu comme Drop + Add | Perte de données. Utilisez `migrationBuilder.RenameColumn`. |

Exemple de correction — ajouter une colonne obligatoire à une table peuplée :

```csharp
// Généré : échoue si la table contient des lignes
migrationBuilder.AddColumn<string>(
    name: "CodeService", table: "Budgets", nullable: false);

// Corrigé
migrationBuilder.AddColumn<string>(
    name: "CodeService", table: "Budgets",
    maxLength: 20, nullable: false, defaultValue: "");
```

### Appliquer

```bash
dotnet ef database update
```

### Annuler la dernière (non appliquée)

```bash
dotnet ef migrations remove
```

**Si elle est déjà appliquée**, revenez d'abord à la précédente :

```bash
dotnet ef database update NomMigrationPrecedente
dotnet ef migrations remove
```

### Produire un script SQL pour la production

C'est la méthode recommandée : l'exploitant relit et exécute, l'application ne touche
jamais au schéma.

```bash
dotnet ef migrations script --idempotent --output migration.sql
```

`--idempotent` génère des gardes `IF NOT EXISTS` : le script est rejouable sans risque.

En production, mettez alors `"MigrerAuDemarrage": false`.

---

## Changer de moteur de base

Le socle utilise SQL Server, mais rien n'y est spécifique hors du fournisseur.

**PostgreSQL :**

```bash
dotnet remove package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

Dans `ServiceCollectionExtensions` :

```csharp
services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(chaine, sql =>
    {
        sql.EnableRetryOnFailure(3);
        sql.CommandTimeout(30);
        sql.MigrationsAssembly(typeof(AppDbContext).Assembly.GetName().Name);
    }));
```

Supprimez les migrations existantes et régénérez-les : elles contiennent du SQL
spécifique au fournisseur.

**SQLite** (pratique pour des tests automatisés) :

```csharp
options.UseSqlite("Data Source=eprocessbudget.db");
```

---

## Performance

### Toujours `AsNoTracking` en lecture seule

```csharp
var items = await ctx.Budgets.AsNoTracking().ToListAsync();
```

Sans lui, EF Core garde un instantané de chaque entité pour détecter les modifications :
inutile et coûteux quand on ne fait qu'afficher.

### Paginer côté base

```csharp
// ✅
var items = await q.Skip((page - 1) * taille).Take(taille).ToListAsync();

// ❌ charge toute la table en mémoire
var items = (await q.ToListAsync()).Skip((page - 1) * taille).Take(taille);
```

### Projeter ce qui est affiché

```csharp
// La liste d'audit n'affiche pas les colonnes JSON : ne les chargeons pas.
.Select(a => new LigneAudit(a.DateAction, a.UtilisateurId, a.Matricule,
                            a.Module, a.Action, a.Entite, a.EntiteId, a.EstSucces))
```

### Indexer les colonnes de filtre et de tri

```csharp
e.HasIndex(x => new { x.Statut, x.DateCreation });
```

L'ordre compte : mettez d'abord les colonnes d'égalité, ensuite celles de tri.

### Voir le SQL généré

```jsonc
"Logging": { "LogLevel": { "Microsoft.EntityFrameworkCore.Database.Command": "Information" } }
```

À couper ensuite : c'est très verbeux.

---

## Sauvegarde et purge

**Sauvegarde** — conservez au moins :

| Table | Pourquoi |
|---|---|
| Toute la base | Les données budgétaires de tous les organismes y sont réunies. |
| `aud.JournalAudit`, `auth.JournalConnexion` | Conservation minimale de 12 mois (§ 5.2). |

**Purge** — les notifications grossissent sans fin :

```sql
-- Notifications lues de plus de 6 mois
DELETE FROM [ntf].[Notification]
WHERE EstLue = 1 AND DateLecture < DATEADD(month, -6, GETUTCDATE());
```

**Ne purgez `aud.JournalAudit` qu'après vérification de vos obligations réglementaires.**

---

## Diagnostic

| Erreur | Cause | Solution |
|---|---|---|
| `ConnectionStrings:DefaultConnection est vide` | Chaîne manquante | Renseignez-la (appsettings, user-secrets ou variable d'environnement). |
| `Login failed for user` | Droits SQL insuffisants | Accordez `db_datareader`, `db_datawriter`, `db_ddladmin`. |
| `A network-related or instance-specific error` | Serveur injoignable | Nom d'instance, pare-feu, protocole TCP/IP activé. |
| `The certificate chain was issued by an authority that is not trusted` | Certificat auto-signé | Ajoutez `TrustServerCertificate=True`. |
| `There is already an object named 'X'` | Migration déjà appliquée manuellement | `dotnet ef migrations script --idempotent`, ou synchronisez `__EFMigrationsHistory`. |
| `A second operation was started on this context instance` | `DbContext` partagé entre composants | Utilisez `IDbContextFactory` — jamais d'injection directe de `AppDbContext`. |
| Aucun compte ne peut se connecter | Seed jamais exécuté, ou `Amorcage:MotDePasse` absent | Démarrez une fois avec `MigrerAuDemarrage: true` et `Amorcage:MotDePasse` renseigné. |
| `Invalid object name 'auth.Profil'` | Base créée mais non migrée | Appliquez les migrations (`dotnet ef database update` ou script idempotent). |
| `SET options have incorrect settings: 'QUOTED_IDENTIFIER'` | Script exécuté par `sqlcmd` sans `-I` | Relancez avec `sqlcmd -I`, ou depuis SSMS. |
| Une requête métier renvoie toujours 0 ligne | `OrganismeCourantId` non renseigné sur le contexte | Renseignez-le (voir *Multi-organisme*). |
| `aucun organisme. Renseignez OrganismeCourantId` | Création d'une entité métier sans organisme courant | Idem. |

