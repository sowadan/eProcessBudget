# 06 — Notifications

Prévenir un utilisateur qu'il se passe quelque chose : dans l'application, et par email.

---

## Ce qui fonctionne, et dans quelles conditions

| | Sans base de données | Avec base de données |
|---|---|---|
| Création d'une notification | ✅ | ✅ |
| Badge et volet de la cloche | ✅ | ✅ |
| Push temps réel + toast | ✅ | ✅ |
| Page `/notifications`, filtres | ✅ | ✅ |
| Marquage lu/non lu, suppression | ✅ | ✅ |
| Paramétrage des canaux | ✅ (non persistant) | ✅ |
| Email | ✅ si SMTP configuré | ✅ si SMTP configuré |
| **Survie au redémarrage** | ❌ | ✅ |

Tout fonctionne dès le premier lancement. **Seule la durée de vie change.**

---

## Envoyer une notification

```csharp
@inject INotificationService Notifications

await Notifications.NotifierAsync(
    type:           TypeNotification.ActionRequise,
    message:        "La commande C-2024-018 attend votre validation.",
    destinataireId: responsableId,
    titre:          "Validation requise",
    lien:           "/commandes/18",
    cleEntite:      "Commande:18",
    acteurId:       moiMeme);
```

| Paramètre | Rôle |
|---|---|
| `type` | Détermine l'icône, la couleur, le libellé par défaut et la ligne de paramétrage. |
| `message` | Une phrase complète et autonome. Elle sera lue hors contexte, dans un volet ou un email. |
| `destinataireId` | L'utilisateur notifié. |
| `titre` | Titre court en gras. Vide → le libellé du type est utilisé. |
| `lien` | Chemin **relatif** ouvert au clic. Menez à l'objet concerné, pas à une liste. |
| `cleEntite` | Référence libre `"Entite:Id"`, pour retrouver les notifications d'un objet. |
| `acteurId` | Qui a déclenché l'action. **Empêche l'auto-notification par email.** |

### À plusieurs destinataires

```csharp
await Notifications.NotifierGroupeAsync(
    type:             TypeNotification.Information,
    message:          "La campagne budgétaire 2025 est ouverte.",
    destinatairesIds: membresIds,
    titre:            "Campagne ouverte",
    lien:             "/campagnes/2025");
```

Une seule écriture en base pour tout le lot, un seul email en copie cachée.

### Écrire un bon message

| ✅ | ❌ | Pourquoi |
|---|---|---|
| « La commande C-2024-018 (1 250 000 F) attend votre validation. » | « Nouvelle notification » | Le message est lu sans contexte. |
| « Votre demande de congés du 12/03 a été refusée : période de clôture. » | « Demande refusée » | Le motif évite un aller-retour. |
| lien vers `/commandes/18` | lien vers `/commandes` | Menez à l'objet, pas à la liste. |

---

## Stockage des notifications

Les notifications sont stockées en base (`NotificationStoreEf`, table
`ntf.Notification`). Le service, la cloche et la page ne connaissent que
`INotificationStore`.

### La structure attendue

Deux tables, créées par la migration du socle :

```sql
CREATE TABLE [ntf].[Notification] (
    Id               binary(16)     NOT NULL,   -- ULID
    UtilisateurId    binary(16)     NOT NULL,   -- destinataire
    OrganismeId      binary(16)     NULL,       -- null = niveau plateforme
    Type             int            NOT NULL,   -- valeur de TypeNotification
    Titre            nvarchar(200)  NULL,
    Message          nvarchar(1000) NOT NULL,
    EstLue           bit            NOT NULL,
    DateLecture      datetime2(3)   NULL,
    Lien             nvarchar(400)  NULL,       -- chemin relatif
    CleEntite        nvarchar(100)  NULL,       -- "Engagement:01JABCD..."
    ActeurId         binary(16)     NULL,

    -- Champs hérités de BaseEntity
    DateCreation     datetime2(3)   NOT NULL CONSTRAINT DF_Notification_DateCreation DEFAULT SYSUTCDATETIME(),
    DateModification datetime2(3)   NULL,
    CreePar          binary(16)     NULL,
    ModifiePar       binary(16)     NULL,
    EstSupprime      bit            NOT NULL,
    DateSuppression  datetime2(3)   NULL,
    SupprimePar      binary(16)     NULL,
    RowVersion       rowversion     NOT NULL,

    CONSTRAINT PK_Notification PRIMARY KEY (Id),
    CONSTRAINT FK_Notification_Utilisateur_UtilisateurId
        FOREIGN KEY (UtilisateurId) REFERENCES [auth].[Utilisateur](Id),
    CONSTRAINT FK_Notification_Organisme_OrganismeId
        FOREIGN KEY (OrganismeId) REFERENCES [plat].[Organisme](Id)
);

-- Index qui sert la cloche : « mes non-lues, les plus récentes »
CREATE INDEX IX_Notification_UtilisateurId_EstLue_DateCreation
    ON [ntf].[Notification] (UtilisateurId, EstLue, DateCreation);

-- [ntf].[ParametreNotification] : Id, Type (int, UX), CanalInterneActif, CanalEmailActif
-- + les mêmes champs hérités de BaseEntity.
```

> Vous n'avez normalement pas à écrire ce SQL : `dotnet ef database update` le fait.
> Il est donné pour que vous sachiez exactement ce qui est attendu — par exemple si la
> base est créée par une équipe d'exploitation à partir d'un script.

`ntf.ParametreNotification` se remplit toute seule : une ligne est créée pour chaque valeur
manquante de `TypeNotification` au démarrage et à l'ouverture de l'écran de paramétrage.
Ajouter un type ne demande donc **aucune migration de données**.

### Purge

La table grossit. Prévoyez une purge périodique :

```sql
DELETE FROM [ntf].[Notification]
WHERE EstLue = 1 AND DateLecture < DATEADD(month, -6, GETUTCDATE());
```

---

## Ajouter un type de notification

**1. La valeur** — `Core/Enums/TypeNotification.cs` :

```csharp
public enum TypeNotification
{
    Information = 0, Succes = 1, Avertissement = 2,
    Erreur = 3, ActionRequise = 4, Rappel = 5,

    // ── Vos types (100+) ──────────────────────────────────────────
    CommandeAValider = 100,
    CommandeRejetee  = 101,
}
```

Comme pour les droits : **les valeurs sont persistées, ne les renumérotez jamais.**

**2. L'apparence** — `Web/Components/UI/NotificationHelpers.cs`, trois méthodes :

```csharp
public static string Libelle(TypeNotification type) => type switch
{
    TypeNotification.CommandeAValider => "Commande à valider",
    TypeNotification.CommandeRejetee  => "Commande rejetée",
    // …
};

public static string Icone(TypeNotification type) => type switch
{
    TypeNotification.CommandeAValider => Icons.Material.Outlined.Gavel,
    TypeNotification.CommandeRejetee  => Icons.Material.Filled.Cancel,
    // …
};

public static (string Fond, string Texte) Couleurs(TypeNotification type) => type switch
{
    TypeNotification.CommandeAValider => ("#FEF3C7", "#92400E"),
    TypeNotification.CommandeRejetee  => ("#FEE2E2", "#991B1B"),
    // …
};
```

**3. Le libellé des emails** — `Infrastructure/Services/Notifications/NotificationEmailFormatter.cs`,
méthode `Libelle`.

Le cas `default` évite un écran cassé si vous oubliez une étape, mais la notification
aura une apparence générique.

---

## Le push temps réel

```
CommandeService
      │ NotifierAsync()
      ▼
NotificationService ──► INotificationStore   (persistance)
      │
      └──────────────► INotificationDispatcher.PublierAsync()
                              │
                              ▼
                  NotificationBell du destinataire
                    (si sa page est ouverte)
                       badge + toast
```

`NotificationDispatcher` est un bus publication/abonnement **en mémoire**, enregistré en
singleton. `NotificationBell` s'y abonne à l'initialisation et s'en désabonne dans
`Dispose` — sans quoi chaque navigation laisserait un abonné fantôme.

En complément, la cloche rafraîchit son compteur **toutes les 45 secondes**. Ce filet
couvre ce que le push ne voit pas.

### Limite en multi-instances

Le bus est local au processus. Avec plusieurs instances derrière un répartiteur de
charge, un utilisateur connecté à l'instance A ne reçoit pas en push une notification
créée par l'instance B — le rafraîchissement de secours la ramènera dans les 45 secondes.

Pour un push strict, branchez un backplane (Redis, Azure SignalR) derrière
`INotificationDispatcher`. **L'interface ne change pas**, donc rien d'autre non plus.

---

## Le canal email

### Activer

```jsonc
"Email": {
  "Active": true,
  "Host": "smtp.interne.local",
  "Port": 25,
  "UseSsl": false,
  "ExpediteurApplicatif": "notifications@monentreprise.local",
  "NomExpediteurApplicatif": "eProcessBudget — Notifications",
  "BaseUrl": "https://eprocessbudget.exemple.local"
}
```

Identifiants SMTP, si nécessaires, en variables d'environnement :

```bash
Email__User="compte-smtp"
Email__Password="motdepasse"
```

Puis, dans `/administration/notifications`, activez le canal email **type par type**.
`Email:Active = false` coupe tout, quel que soit ce réglage : c'est l'interrupteur
général.

> **`BaseUrl` est important.** Sans elle, les emails partent sans bouton d'action :
> le socle refuse d'envoyer un lien relatif, qui serait mort dans une messagerie.

### Envoi asynchrone

```
NotificationService ──► IEmailQueue.Enqueue()   ← retour immédiat
                              │
                              ▼
                    EmailQueueProcessor (service d'arrière-plan)
                              │  réessais bornés
                              ▼
                        SmtpEmailSender
```

L'action métier ne subit jamais la latence SMTP. Un relais lent ou en panne ne fait pas
échouer une validation de commande — au pire l'email est perdu, la notification interne
reste.

**Limites assumées :** la file est en mémoire et bornée à 2000 messages. Un arrêt brutal
perd les emails en attente ; sous saturation, les plus anciens sont abandonnés. Pour une
garantie stricte, remplacez `EmailQueue` par une file durable (table SQL, broker).

### Le choix de l'expéditeur

| Élément | Valeur | Pourquoi |
|---|---|---|
| `From` (adresse) | Toujours le compte applicatif | Usurper l'adresse d'un utilisateur ferait rejeter le message par SPF/DMARC. |
| `From` (nom affiché) | « Jean Dupont (via eProcessBudget) » | L'utilisateur voit qui est à l'origine de l'action. |
| `Reply-To` | Adresse de l'acteur | Une réponse arrive à la bonne personne. |
| Destinataires | En **copie cachée** | Les destinataires ne voient pas les adresses des autres. |

### Ne pas s'auto-notifier

Renseignez `acteurId` : le destinataire égal à l'acteur est retiré de la liste des
emails. Personne n'a besoin d'un email lui annonçant ce qu'il vient de faire.

La notification **interne**, elle, est créée — elle reste une trace utile dans
l'historique.

---

## Paramétrer les canaux

`/administration/notifications` (droit `NotificationParametrage`, réservé à l'Administrateur
plateforme : les canaux valent pour toute l'instance, tous organismes confondus) :

| Canal | Effet |
|---|---|
| **Interne** | Notification stockée + poussée dans la cloche. |
| **Email** | Email envoyé au destinataire. |

Les deux sont indépendants. Un type sans aucun canal ne produit rien : c'est ainsi qu'on
coupe un événement trop bavard **sans toucher au code qui l'émet**.

Le bouton *Test* de chaque ligne s'envoie une notification à soi-même : la façon la plus
rapide de vérifier une configuration de bout en bout.

Les réglages sont mis en cache 5 minutes côté `ParametreNotificationServiceEf` — la
question est posée à chaque création de notification.

---

## Notifications planifiées

Le socle ne fournit pas d'ordonnanceur, mais le motif tient en un service d'arrière-plan :

```csharp
public class RelanceEcheancesWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Un scope par itération : un BackgroundService est singleton,
            // il ne peut pas dépendre directement de services Scoped.
            using (var scope = _scopes.CreateScope())
            {
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                // … repérer les échéances et notifier
            }

            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
```

Enregistrement : `builder.Services.AddHostedService<RelanceEcheancesWorker>();`

> **Pensez à l'idempotence.** Un traitement horaire enverrait 24 rappels par jour pour la
> même échéance. Tenez un journal des envois (clé `"Commande:18:RappelJ-2"`) et vérifiez
> avant d'émettre.

---

## Diagnostic

| Symptôme | Cause probable | Que faire |
|---|---|---|
| La cloche ne bouge pas | Canal interne désactivé pour ce type | Vérifier `/administration/notifications`. |
| Aucun email | `Email:Active = false`, ou canal email coupé pour ce type | Vérifier les deux niveaux. |
| Email sans bouton d'action | `Email:BaseUrl` vide | Renseigner l'URL publique. |
| Pas d'email pour ses propres actions | **Comportement voulu** | Ne pas passer `acteurId` si l'on veut vraiment se notifier. |
| Badge en retard de 45 s | Push manqué, filet de sécurité | Normal en multi-instances. Voir « Limite en multi-instances ». |
| `Échec d'envoi email (tentative 1/3)` | Relais injoignable | Vérifier `Host`, `Port`, le pare-feu, les identifiants. |

