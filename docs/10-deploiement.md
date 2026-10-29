# 10 — Déploiement

Ce qu'il faut vérifier avant de mettre en production, et comment publier.

---

## Liste de contrôle avant mise en production

> **Authentification par cookie.** Le cookie de session est `Secure` hors développement :
> l'application **doit** être servie en HTTPS, sans quoi aucune connexion ne tient.
> Voir [04 — Plateforme et authentification](04-authentification.md).

### Sécurité — bloquant

- [ ] **Compte d'amorçage** initialisé (`Amorcage__MotDePasse` le temps du premier
      démarrage, puis variable retirée) et son mot de passe changé après la première connexion.
- [ ] **Clés Data Protection persistantes et partagées** : elles chiffrent le cookie de
      session et les liens de réinitialisation. Sous IIS, activez « Charger le profil
      utilisateur » du pool d'applications ; derrière plusieurs serveurs, partagez les clés
      (`AddDataProtection().PersistKeysToFileSystem(...)`), sinon chaque redémarrage ou
      chaque serveur déconnecte les utilisateurs.
- [ ] **`Email:BaseUrl`** renseignée : elle sert à construire les liens de réinitialisation.
- [ ] **`AllowedHosts`** restreint aux noms réels du site (`eprocessbudget.mondomaine.org;srv-app`),
      dans `appsettings.Production.json` ou la variable `AllowedHosts` : avec `*`, toute valeur
      d'en-tête `Host` est acceptée. Un avertissement est journalisé au démarrage tant qu'il vaut `*`.
- [ ] **Derrière un reverse proxy** : `UseForwardedHeaders` configuré, sinon le journal des
      connexions et la limitation de débit voient l'adresse du proxy au lieu de celle du poste.
- [ ] **`DetailedErrors = false`** dans `appsettings.Production.json`.
- [ ] **`web.config` sans diagnostic** : `stdoutLogEnabled="false"` et variable
      `ASPNETCORE_DETAILEDERRORS` retirée.
- [ ] **Aucun secret dans un fichier versionné.** Mot de passe SMTP et chaîne de
      connexion en variables d'environnement.
- [ ] **HTTPS actif.**

### Base de données

- [ ] `ConnectionStrings__DefaultConnection` défini en variable d'environnement.
- [ ] `Persistance:MigrerAuDemarrage = false` — la migration est pilotée par la chaîne de
      déploiement, pas par l'application au démarrage.
- [ ] Script de migration **généré, relu et exécuté** :
      `dotnet ef migrations script --idempotent`.
- [ ] **Premier déploiement uniquement :** données de référence et premier utilisateur
      créés — voir « Premier déploiement » ci-dessous.
- [ ] Compte SQL applicatif limité à `db_datareader` + `db_datawriter`
      (pas de `db_ddladmin` si l'application ne migre pas).
- [ ] Sauvegarde configurée et **restauration testée**.

### Application

- [ ] Section `Application` renseignée : nom, sigle, description, couleurs, logo.
- [ ] Feuille Tailwind **à jour** (`npm run css` après toute modification d'interface, ou
      compilation avec `node_modules` présent) — voir [07 — Composants d'interface](07-composants-ui.md).
- [ ] `Logging:LogLevel:Default` à `Warning`, votre namespace à `Information`.

### Notifications

- [ ] `Email:Active` cohérent avec la disponibilité du relais SMTP.
- [ ] `Email:BaseUrl` = URL publique réelle — sinon les emails partent sans bouton.
- [ ] Envoi de test effectué depuis `/administration/notifications`.

---

## Configuration de production

`appsettings.Production.json` — **sans aucun secret** :

```jsonc
{
  "Persistance": {
    "MigrerAuDemarrage": false
  },

  "DetailedErrors": false,

  "Email": {
    "Active": true,
    "Host": "smtp.exemple.local",
    "Port": 25,
    "ExpediteurApplicatif": "notifications@exemple.local",
    "NomExpediteurApplicatif": "eProcessBudget",
    "BaseUrl": "https://eprocessbudget.exemple.local"
  },

  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "eProcessBudget": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Les secrets, en variables d'environnement du serveur :

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Data Source=SRV-SQL;Initial Catalog=eProcessBudget;Integrated Security=True;TrustServerCertificate=True
Email__User=compte-smtp
Email__Password=...
```

---

## Publier

```bash
dotnet publish eProcessBudget.Web/eProcessBudget.Web.csproj -c Release -o ./publish
```

Le dossier `publish/` contient tout le nécessaire.

### IIS

**Prérequis :** [ASP.NET Core Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0) **.NET 10**
installé sur le serveur — il apporte le module qui fait le pont entre IIS et Kestrel.

1. Créer un pool d'applications en mode **« Code managé : Aucun »** (Kestrel héberge le
   runtime, IIS ne sert que de proxy).
2. Identité du pool : un compte ayant accès à SQL Server si vous utilisez
   `Integrated Security=True`.
3. Déposer `publish/` dans le répertoire du site.
4. Lier un certificat TLS et forcer HTTPS.

Le projet versionne son propre `eProcessBudget.Web/web.config`, repris par `dotnet publish`.
Hors diagnostic, il doit ressembler à ceci :

```xml
<aspNetCore processPath="dotnet" arguments=".\eProcessBudget.Web.dll"
            stdoutLogEnabled="false" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  </environmentVariables>
</aspNetCore>
```

> Les secrets sont mieux placés dans les variables d'environnement **du pool
> d'applications** (Gestionnaire IIS → Configuration éditeur) que dans `web.config`,
> qui est un fichier déployé.

**Diagnostiquer une erreur 500.30** (l'application ne démarre pas) : passez
temporairement `stdoutLogEnabled="true"` (journaux dans `.\logs\stdout_*.log`, le pool
doit pouvoir y écrire) et ajoutez la variable `ASPNETCORE_DETAILEDERRORS=true`. Les causes
les plus fréquentes : chaîne de connexion absente, compte du pool sans accès SQL. **Remettez les deux réglages à leur valeur normale
une fois le problème trouvé** : ces fichiers ne sont jamais purgés et la page d'erreur
expose la pile d'appels.

**Identité du pool et accès SQL.** Avec `Integrated Security=True`, c'est l'identité du
pool qui se connecte à SQL Server : le compte de service du domaine s'il est configuré,
sinon — pour `ApplicationPoolIdentity` vers un SQL Server distant — le compte machine
`DOMAINE\SERVEUR$`. Il lui faut un login SQL et les rôles `db_datareader` +
`db_datawriter` sur la base de l'application.

### Blazor Server derrière un reverse proxy

Blazor Server maintient une connexion **WebSocket** permanente par onglet ouvert. Sans
cela, l'application se dégrade en long-polling : lente et instable.

Pour IIS, activer la fonctionnalité **WebSocket Protocol** (Ajout de rôles et
fonctionnalités → Serveur Web → Développement d'applications).

Pour nginx :

```nginx
location / {
    proxy_pass         http://localhost:5000;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_set_header   Host $host;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;

    # La connexion du circuit est longue : un timeout court la coupe sans cesse.
    proxy_read_timeout 100s;
}
```

Derrière un proxy qui termine le TLS, ajoutez dans `Program.cs` :

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

Sans cela, l'application se croit en HTTP et peut poser un cookie sans l'attribut
`Secure`.

---

## Migrer la base au déploiement

**Méthode recommandée** — script relu par l'exploitant :

```bash
cd eProcessBudget.Infrastructure
dotnet ef migrations script --idempotent --output migration.sql
```

`--idempotent` produit des gardes `IF NOT EXISTS` : le script est rejouable sans risque.

Ordre d'une mise à jour :

1. sauvegarder la base ;
2. arrêter l'application ;
3. exécuter `migration.sql` ;
4. déployer les binaires ;
5. redémarrer ;
6. vérifier les journaux de démarrage.

### Premier déploiement : données de référence et premier utilisateur

Sur une base neuve, les migrations créent les tables mais **aucune donnée** : ni profil,
ni utilisateur. Le seed qui les crée ne tourne que si `MigrerAuDemarrage = true`, ce qui
n'est pas le cas en production. Procédure :

1. exécuter `migration.sql` sur la base de production ;
2. définir **temporairement** la variable `Persistance__MigrerAuDemarrage=true` sur le
   pool d'applications (le compte SQL du pool doit alors avoir `db_ddladmin`) ;
3. démarrer l'application et vérifier dans les journaux « Premier utilisateur … créé »
   et « Base de données prête » ;
4. retirer la variable (et `db_ddladmin`), redémarrer.

Même procédure (étapes 2 à 4) quand vous ajoutez des droits à `TypeDroit` et voulez
qu'ils soient accordés automatiquement au profil `ADMIN`.

---

## Surveillance

### Ce qu'il faut suivre

| Indicateur | Où | Seuil d'alerte |
|---|---|---|
| Erreurs applicatives | Journaux, niveau `Error` | toute occurrence |
| Échecs d'envoi email | Journaux, `EmailQueueProcessor` | série d'échecs |
| Croissance des tables | `ntf.Notification`, `aud.JournalAudit`, `auth.JournalConnexion` | purge à prévoir |
| Circuits Blazor actifs | Compteurs .NET | proportionnel aux onglets ouverts |

### Requêtes utiles

```sql
-- Actions refusées faute de droits
SELECT TOP 50 DateAction, UtilisateurMatricule, Module, Action, MessageErreur
FROM [aud].[JournalAudit]
WHERE EstSucces = 0
ORDER BY DateAction DESC;
```

### Point de contrôle de santé

Utile pour un répartiteur de charge. Dans `Program.cs` :

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();
// …
app.MapHealthChecks("/sante");
```

---

## Mise à l'échelle

Blazor Server garde l'état de chaque onglet **en mémoire du serveur**. Deux conséquences
en multi-instances :

| Point | Conséquence | Solution |
|---|---|---|
| **Affinité de session** | Un circuit doit toujours revenir sur la même instance | Activer les sessions persistantes sur le répartiteur. |
| **Push de notification** | `NotificationDispatcher` est local au processus | Le rafraîchissement de 45 s prend le relais. Pour du push strict, brancher un backplane derrière `INotificationDispatcher`. |

La file d'emails est aussi locale et non persistante : un arrêt brutal perd les messages
en attente. Pour une garantie stricte, remplacez `EmailQueue` par une file durable.

En pratique, une instance unique suffit largement.

---

## Après le déploiement

Vérifiez dans cet ordre :

1. **Les journaux de démarrage** — pas d'exception, base initialisée.
2. **L'accueil** s'affiche avec vos couleurs et votre logo, au nom de l'utilisateur courant.
3. **Le menu** correspond aux droits de l'utilisateur courant.
4. **Une notification de test** depuis `/administration/notifications`.
5. **Le journal d'audit** contient les actions que vous venez de faire.

Si ces cinq points passent, le socle est opérationnel.

