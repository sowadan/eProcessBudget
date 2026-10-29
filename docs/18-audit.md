# 18 — Audit et traçabilité

Le neuvième module (cahier fonctionnel § 3.13, technique § 2.7 et 4.9) : la traçabilité de
toutes les opérations, la consultation et l'export du journal, la rétention des journaux.

| Droit | Permet |
|---|---|
| `AuditConsultation` (« AuditLire » du cahier) | Écran *Administration › Journal d'audit* (et journal des connexions) |
| `AuditExportation` | Export Excel / PDF du journal |

Un utilisateur d'organisme ne voit que les lignes de son organisme ; un Administrateur
plateforme, celles du niveau plateforme.

---

## Ce qui est tracé

| Source | Contenu | Module |
|---|---|---|
| **Journalisation automatique** (enregistrement du `DbContext`) | Toute création, modification, suppression logique ou suppression d'une table : auteur (colonnes `CreePar` / `ModifiePar` / `SupprimePar`), date UTC, adresse IP, corrélation, élément et identifiant ; valeurs **avant / après** en JSON (champs modifiés seulement pour une modification) | `Données` |
| Événements métier (services) | Validations, rejets, exports, clôtures, attributions de droits, purges... avec leur commentaire | module du service |
| Journal des connexions | Connexions, échecs, blocages, déconnexions | `auth.JournalConnexion` |

Les lignes automatiques et les événements métier se complètent : la première dit **quels
champs** ont changé, le second **pourquoi** (l'acte de gestion). L'écran filtre l'un ou l'autre
(« Type »).

### Exclusions

- Les journaux eux-mêmes et les notifications.
- Les champs sensibles : `PasswordHash`, `SecurityStamp`, tout champ de jeton, le contenu et
  l'empreinte des fichiers.
- Les colonnes techniques (dates et auteurs de création / modification, `RowVersion`) : elles
  servent à désigner l'auteur.
- Le suivi de connexion d'un utilisateur (dernière connexion, compteur d'échecs, blocage) : le
  journal des connexions le trace déjà. Une modification sans autre changement n'écrit rien.

### Adresse IP

En Blazor Server, une fois le circuit ouvert, il n'y a plus de requête HTTP. L'adresse est donc
relevée à l'ouverture du circuit (`App.razor` la passe à `Routes`, qui la dépose dans le service
scoped `IContexteConnexion`) et gardée pour sa durée ; les requêtes HTTP (points de terminaison)
la déposent par un intergiciel. La fabrique de contexte la reporte sur chaque `AppDbContext`.
Derrière un proxy inverse, configurer les en-têtes transférés (`UseForwardedHeaders`) pour
obtenir l'adresse du client plutôt que celle du proxy.

La **corrélation** vaut l'identifiant de la requête HTTP, ou un identifiant propre au circuit :
toutes les lignes d'une même session de travail le partagent.

---

## Les règles

| Règle | Où |
|---|---|
| Journal automatique écrit dans la **même transaction** que les écritures qu'il décrit | `AppDbContext.PreparerEnregistrement`, `JournalisationAutomatique` |
| **Ajout seul** : toute modification ou suppression d'une ligne du journal d'audit ou de connexion est refusée (exception) | `JournalisationAutomatique.VerifierAjoutSeul` |
| Seule exception : la **purge planifiée** au-delà de la rétention, journalisée (action `PURGE`) | `PurgeJournaux` |
| Rétention : paramètre de l'organisme (*Paramétrage › Organisme*, 12 à 120 mois), **12 mois au minimum** ; niveau plateforme : 12 mois | `PurgeJournaux` |
| Purge chaque **dimanche à 4 h**, tous organismes | `TraitementPurgeJournaux` |
| La suppression logique renseigne `DateSuppression` | `AppDbContext.PreparerEnregistrement` |
| Export : `AuditExportation` et `AuditConsultation` ; 10 000 lignes au plus (les plus récentes) ; filtres en en-tête ; export journalisé | `JournalAuditService.ExporterAsync` |

---

## L'écran

*Administration › Journal d'audit* :

- **filtres** : période (jours locaux), utilisateur, recherche (identifiant, matricule, adresse
  IP, commentaire), type (écritures automatiques / événements métier), module, action, élément
  (table), résultat ;
- **liste** : date, utilisateur, adresse IP, module, action, élément, résultat ; un clic ouvre le
  détail ;
- **détail** : auteur, adresse IP, corrélation, navigateur, commentaire, erreur, et les **valeurs
  avant / après côte à côte** (valeur barrée en rouge, nouvelle en vert, option « champs modifiés
  seulement ») ;
- **export** Excel ou PDF de la sélection, avec l'en-tête institutionnel des rapports
  ([17](17-rapports.md)).

```
Infrastructure/
├── Data/JournalisationAutomatique.cs        lignes automatiques, ajout seul, exclusions
├── Data/AppDbContext.cs                     branchement à l'enregistrement ; AdresseIp, CorrelationId
└── Services/Audit/
    ├── ContexteConnexion.cs                 adresse IP et corrélation de la session
    ├── JournalAuditService.cs               consultation, détail champ par champ, export
    └── PurgeJournaux.cs                     purge et traitement hebdomadaire

Web/Features/Administration/
├── Audit.razor                              écran du journal
└── DialogDetailAudit.razor                  détail et valeurs côte à côte
```

---

## Écarts avec le cahier technique

| Point | Choix |
|---|---|
| « Intercepteur SaveChanges » | La journalisation est branchée dans la surcharge de `SaveChanges` du `AppDbContext`, qui portait déjà les contrôles d'enregistrement : même effet, active partout (tests compris), sans dépendance à l'injection. |
| `Action` en TINYINT (`ActionAudit`) | Le socle stocke l'action en texte (`CREATION`, `VALIDATION`...) : lisible tel quel, et les services l'emploient déjà. |
| Colonnes du socle | Le journal du socle avait déjà les valeurs avant / après en JSON, l'adresse IP, l'organisme et la corrélation : aucune migration. |

---

## Tests

`eProcessBudget.Tests/AuditTests.cs` : création, modification (champs modifiés seulement) et
suppression logique journalisées avec auteur, organisme, adresse IP et corrélation ; champs
sensibles et suivi de connexion non journalisés ; journaux en ajout seul ; purge au-delà de la
rétention (minimum 12 mois) et journalisée ; contexte de connexion ; consultation filtrée,
cloisonnée par organisme, détail champ par champ ; comparaison avant / après ; export Excel et
PDF soumis au droit, avec filtres en en-tête et trace.
