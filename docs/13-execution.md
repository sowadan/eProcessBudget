# 13 — Cycle budgétaire commun (exécution)

Le quatrième module (cahier fonctionnel § 3.4, technique § 4.4) : le cycle de gestion
partagé par les quatre axes de dépenses : prévisions initiales, ajustements,
engagements (AE / AD), dépassements, factures, paiements, rejets et pénalités, alertes,
clôture d'exercice. Les axes (Entretien routier, Investissements, Fonctionnement, Appuis)
n'ajoutent que leurs spécificités ([14](14-axes-depenses.md)).

| Droit | Permet |
|---|---|
| `ExecutionLire` | Consulter tous les écrans du module |
| `DotationSaisir` | Saisir les prévisions initiales |
| `AjustementSaisir` / `AjustementValider` | Saisir (soumettre) / valider ou rejeter les ajustements |
| `EngagementSaisir` | Engagements, échéancier des CP, rejets et pénalités, factures |
| `EngagementValider` | Valider, rejeter, annuler les engagements (Responsable Budget) |
| `DepassementValider` | Décider des dépassements (Responsable Budget) |
| `PaiementSaisir` | Paiements et factures (Comptable) |
| `ExerciceCloturer` | Clôturer un exercice |

Chacun de ces droits donne aussi la lecture (`AccesExecution.Lecture`).

---

## Les écrans

| Menu | Route | Contenu |
|---|---|---|
| Dotations | `/execution` | Dotations de l'exercice et leur situation ; totaux ; saisie des prévisions initiales |
| (fiche) | `/execution/dotations/{id}` | Situation complète (composant `CarteSituation`) ; engagements, ajustements, reports reçus |
| Ajustements | `/execution/ajustements` | Hausses et baisses ; validation et rejet |
| Engagements | `/execution/engagements` | AE et AD ; filtres statut, type, bénéficiaire |
| (fiche) | `/execution/engagements/{id}` | Soumission, validation, annulation ; paiements, factures, rejets et pénalités, échéancier des CP, dépassements |
| Dépassements | `/execution/depassements` | File des dépassements en attente de décision |
| Paiements | `/execution/paiements` | Historique par ligne, projet, entité, période |
| Clôture | `/execution/cloture` | Aperçu (reliquats, encours, blocages) puis clôture |

`Features/Execution/Commun/CarteSituation.razor` est **le** composant « situation d'une
dotation » du cahier technique : les axes de dépenses le réutilisent tel quel (fiche projet).

---

## Comment c'est construit

```
Core/Enums/CalculsExecution.cs   MontantsDotation : toutes les formules du § 4.1 ; contrôles ; libellés

Infrastructure/Services/Execution/
├── AccesExecution.cs       droits, périmètre (axe, structure, site de la dotation), exercice ouvert, numérotation
├── SituationService.cs     listes et fiche des dotations, montants calculés en base
├── DotationService.cs      prévisions initiales
├── AjustementService.cs    hausses et baisses, validation
├── EngagementService.cs    engagements, soumission, validation, échéancier des CP, rejets et pénalités
├── DepassementService.cs   file et décision
├── PaiementService.cs      paiements, factures, historique
├── AlerteService.cs        seuils d'alerte ; TraitementAlertesNocturne (BackgroundService, 2 h)
└── ClotureService.cs       aperçu et clôture
```

### Où se calculent les montants

`MontantsDotation` (Core) porte les formules : prévision ajustée, dotation totale, restes
à payer, encours à reporter, reliquat, taux sur les deux bases. `SituationService.Calculer`
lit en base, par sous-requêtes, les montants bruts de chaque dotation (pièces **validées**
seulement), avec les mêmes conventions que la vue `bud.vw_SituationDotation`. La vue
reste la source des états et du tableau de bord ; les écrans de saisie et les contrôles
s'appuient sur `Calculer`, ce qui permet de tester toutes les formules sans SQL Server.

---

## Les règles

| Règle | Où |
|---|---|
| Prévision initiale libre en préparation, **figée à l'ouverture** de l'exercice (les dotations passent « Validé ») ; ensuite, ajustements seulement | `ExerciceService.OuvrirAsync`, `DotationService` |
| Dotation créée en cours d'exercice : prévision 0, alimentée par ajustement | `DotationService` |
| Une dotation par imputation (ligne, projet, site, service, entité) et par exercice ; ligne de **dépense** active | `DotationService` |
| Toute saisie : exercice **ouvert**, date dans l'exercice, mois non clos | `AccesExecution.ExerciceOuvertAsync`, `DateSaisissableAsync` |
| Ajustement : montant non nul, **motif et justification obligatoires**, motif compatible avec le sens (hausse / baisse) | `AjustementService` |
| Une baisse ne ramène ni la prévision ajustée sous 0, ni la dotation totale sous les engagements validés (contrôlé à la saisie et à la validation) | `AjustementService.ControlerBaisseAsync` |
| Seuls les ajustements, engagements et paiements **validés** comptent | `SituationService.Calculer` |
| **Dépassement non bloquant** : motif exigé à la soumission, demande `ValidationDepassement` en attente, notification au Responsable Budget ; l'engagement n'est validable qu'après sa décision. Accepter valide l'engagement, refuser le rejette. Un dépassement apparu entre soumission et validation est détecté à la validation | `EngagementService`, `DepassementService` |
| **Paiement ≤ reste à payer** (montant engagé − paiements − rejets ou pénalités), engagement validé, date dans l'exercice, numéro de pièce unique | `PaiementService` |
| Rejet ou pénalité ≤ reste à payer ; ils réduisent l'encours à reporter (C.2.6) | `EngagementService.AjouterRejetAsync` |
| Σ CP de l'échéancier ≤ montant engagé | `EngagementService.DefinirEcheancierAsync` |
| Total facturé ≤ montant engagé | `PaiementService.AjouterFactureAsync` |
| **Tout décaissement se rapporte à une facture reçue** (C.2.2) : hors appuis, un paiement porte sur une facture de l'engagement (ou de son engagement d'origine) et n'en dépasse pas le reste | `PaiementService.EnregistrerAsync` |
| Annulation d'un engagement validé : sans paiement, facture ni rejet | `EngagementService.AnnulerAsync` |
| Un paiement ne se supprime pas : il s'annule, avec un motif | `PaiementService.AnnulerAsync` |

### Numérotation

Ajustements et engagements sont numérotés par l'application, par organisme et par année :
`AJ-2027-00001`, `ENG-2027-00001`. Un numéro n'est jamais réattribué (les pièces supprimées
comptent). Le numéro de pièce d'un paiement est celui du comptable. Un encours reporté
reçoit le numéro de son engagement d'origine préfixé de l'année d'arrivée :
`R2028-ENG-2027-00001`.

### Alertes

Quand le taux d'engagement d'une dotation (y compris reports) atteint un seuil paramétré
(80 / 90 / 100 % par défaut ; ceux de la ligne priment sur ceux de l'axe, qui priment sur
les seuils globaux), les détenteurs de `DepassementValider` ou `EngagementValider` sont
notifiés : **une fois** par dotation et par seuil (clé `Dotation:{id}:Seuil:{s}`). Contrôle
après chaque validation (engagement, baisse, dépassement accepté) et chaque nuit à 2 h
pour tous les organismes actifs.

### Clôture

Préalables : exercice ouvert, exercice suivant créé (en préparation ou ouvert), aucune
pièce en attente (ajustement soumis, engagement en brouillon ou soumis, dépassement en
attente). La clôture, en une seule opération :

1. crée sur l'exercice suivant les dotations de même imputation qui manquent (prévision 0) ;
2. reporte chaque engagement non soldé : engagement `EstReport = 1` du montant de son
   encours, et report « encours » du même montant sur la dotation ;
3. reporte en « reliquat de crédit » ce qui n'est ni payé ni en encours ;
4. verse l'exécution des dépenses et des recettes à l'historique (`hist.ExecutionHistorique`, par axe, rubrique, nature, site ; voir [15](15-recettes.md)) ;
5. clôture l'exercice et ses périodes. Elle est journalisée.

---

## Écarts avec le cahier technique

| Point | Choix |
|---|---|
| Clôture « exécutée en tâche de fond » | Exécutée pendant la demande, en une seule écriture : quelques secondes pour un exercice courant, et un échec ne laisse rien à moitié fait. À déplacer dans une file si les volumes l'exigent. |
| Report négatif d'un dépassement (C.2.5) | Non activé : un reliquat n'est jamais négatif. |
| Équilibre des reports de trésorerie (C.2.7) | Affiché dans l'aperçu de clôture (grandeurs A à I, contrôle H = I) ; voir [19](19-reponses-metier.md). |

## Pas encore couvert

| Point | Dépend de |
|---|---|
| Paiements par activité (via les clés de répartition) | Module Consommation budgétaire (`prog.vw_ConsommationActivite`) |
| Exports PDF / Excel | Module Rapports |

---

## Tests

`eProcessBudget.Tests/ExecutionTests.cs` : toutes les formules du § 4.1 sur un cas chiffré
(deux bases, « n.s. »), dépassement, seuils et numérotation ; prévision figée à
l'ouverture puis ajustement motivé et validé, baisse refusée sous les engagements ;
dépassement non bloquant mais en attente de décision, puis accepté, avec alerte émise une
seule fois ; paiement refusé au-delà du reste à payer, hors exercice ou en doublon,
pénalité qui réduit le payable ; clôture bloquée par une pièce en attente, puis clôture
avec reliquat, encours reporté, dotation créée et historique.
