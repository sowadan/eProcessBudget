# 15 — Recettes

Le sixième module (cahier fonctionnel § 3.9, technique § 4.6) : prévision et suivi mensuel
des ressources de l'organisme, échéancier de recouvrement, reports de ressources
antérieures, bilan recettes / dépenses.

| Droit | Permet |
|---|---|
| `RecettesLire` | Consulter tous les écrans du module |
| `PrevisionRecetteSaisir` | Prévisions, révisions, échéancier de recouvrement, reports de ressources (profil Responsable Budget) |
| `EncaissementSaisir` | Saisie mensuelle des encaissements (profil Comptable) |

Chaque droit de saisie donne aussi la lecture (`AccesRecettes.Lecture`). Toute saisie exige en
plus une **habilitation de saisie active** couvrant l'axe de la ligne de recette (axe
Recettes) ou le site.

---

## Les écrans

| Menu | Route | Contenu |
|---|---|---|
| Synthèse | `/recettes` | Indicateurs clés ; encaissements mensuels face à N-1 et à l'échéancier ; par catégorie, nature (face à la programmation triennale), site, collecteur ; sites sur trois exercices |
| Prévisions | `/recettes/previsions` | Prévisions par ligne, nature, site, collecteur ; révision ; échéancier |
| Encaissements | `/recettes/encaissements` | Grille prévision × mois ; journal (filtres mois, positionnement) |
| Échéancier | `/recettes/echeancier` | Toutes les échéances de l'exercice : couvertes, à venir, en retard |
| Reports de ressources | `/recettes/reports` | Reliquats d'exercices passés, positionnés ou non |
| Bilan recettes / dépenses | `/recettes/bilan` | Ressources face aux dépenses, en prévision et en réalisation ; trésorerie positionnée ou non |

---

## Comment c'est construit

```
Core/Enums/CalculsRecettes.cs   MontantsRecette (formules de rec.vw_SituationRecette), accroissement, bilan,
                                couverture de l'échéancier

Infrastructure/Services/Recettes/
├── AccesRecettes.cs            droits, périmètre (axe de la ligne, site)
├── PrevisionRecetteService.cs  prévisions, révisions, échéancier ; projection des montants en base
├── EncaissementService.cs      saisie mensuelle, grille, journal
├── ReportRessourceService.cs   reports de ressources antérieures
├── SyntheseRecetteService.cs   regroupements, indicateurs, mensuel, comparaison des sites, échéances, bilan
└── AlerteRecouvrement.cs       échéances dépassées ; TraitementRecouvrementNocturne (BackgroundService, 3 h)
```

Les montants viennent de `PrevisionRecetteService.Projeter`, avec les conventions de la vue
`rec.vw_SituationRecette` (encaissements **validés** seulement ; prévision retenue = révisée
sinon initiale). La vue reste la source des états et du tableau de bord.

---

## Les règles

| Règle | Où |
|---|---|
| Une prévision porte sur une ligne de **recette** active ; sa nature est celle de la ligne ou l'une de ses sous-natures | `PrevisionRecetteService` |
| Nature suivie par site (péage, pesage) : le site est obligatoire (C.2.13) | `PrevisionRecetteService` |
| Collecteur : tiers de type « Organisme collecteur » | `PrevisionRecetteService` |
| Une prévision par ligne, nature, site et collecteur sur l'exercice | `PrevisionRecetteService` |
| Prévision initiale libre en préparation, **figée à l'ouverture** ; en cours d'exercice, une nouvelle prévision naît à 0 | `PrevisionRecetteService` |
| Révision : exercice ouvert, **motif obligatoire**, pas en dessous de l'échéancier | `PrevisionRecetteService.ReviserAsync` |
| Les reports de ressources ne se prévoient pas : ils ont leur écran | `PrevisionRecetteService` |
| Échéancier : dates dans l'exercice, montants > 0, un par jour, total ≤ prévision retenue | `DefinirEcheancierAsync` |
| Encaissement : exercice **ouvert**, date dans l'exercice, mois non clos, montant > 0 ; le mois de rattachement se déduit de la date | `EncaissementService` |
| Un compte bancaire crédité rend l'encaissement **positionné** ; sans compte, la ressource est acquise mais non positionnée | `EncaissementService` |
| Un encaissement est validé d'emblée ; il se corrige ou se supprime tant que son mois n'est pas clos | `EncaissementService` |
| Une prévision encaissée ne se supprime pas ; une prévision figée (exercice ouvert, montant non nul) non plus | `PrevisionRecetteService` |
| Report : nature de la catégorie « Report de ressources antérieures », montant > 0, exercice non clôturé | `ReportRessourceService` |

### Formules (§ 4.1)

| Indicateur | Formule |
|---|---|
| Taux de recouvrement | encaissé / prévision retenue ; « n.s. » si la prévision est nulle |
| Niveau des recettes | total encaissé de l'exercice |
| Taux d'accroissement des ressources | (encaissé N − encaissé N-1) / encaissé N-1, **à période comparable** : jusqu'au dernier mois encaissé de N (l'année entière une fois l'exercice clôturé) |
| Ressources additionnelles mobilisées | encaissé des natures de la catégorie « Ressource additionnelle » |
| Bilan recettes / dépenses | ressources réalisées − dépenses réalisées |

Le bilan compte en **ressources réalisées** les encaissements validés et les reports de
ressources antérieures. Les **dépenses réalisées** sont les paiements validés de l'exercice,
tous axes, encours reportés compris. En prévision, il oppose les ressources prévues (prévisions
retenues et reports) aux dotations totales : c'est l'équilibre du budget.

### Échéancier de recouvrement

Les encaissements d'une prévision couvrent ses échéances **dans l'ordre chronologique**
(`CalculsRecettes.Couverture`). Une échéance est en retard si sa date est passée et que
l'encaissé ne couvre pas le cumul attendu à cette date. Chaque nuit à 3 h, pour les exercices
ouverts des organismes actifs, chaque échéance en retard est notifiée **une fois** aux
détenteurs de `PrevisionRecetteSaisir` et `EncaissementSaisir` (clé `Echeance:{id}:Retard`).

### Comparaison sur trois exercices

Par site, l'encaissé de N, N-1 et N-2 : les encaissements de l'exercice de l'année s'il en
porte, sinon l'historique repris (`hist.ExecutionHistorique`, lignes de recette). Une année
sans aucune donnée affiche « — ».

### Clôture

La clôture d'exercice ([13](13-execution.md)) verse désormais aussi les recettes à
l'historique : prévision retenue et encaissé, par axe, rubrique, nature de recette et site
(`Source = Calcul`). C'est la base des comparaisons pluriannuelles.

---

## Écarts avec le cahier technique

Choix validés le 11/10/2026, de même que le taux d'accroissement à période comparable et la
prise en compte des reports dans les ressources réalisées du bilan.

| Point | Choix |
|---|---|
| Cycle de validation des encaissements | Non prévu par le cahier (§ 4.6 : « saisie mensuelle ») : un encaissement est validé à la saisie, corrigeable tant que son mois est ouvert, puis figé à la clôture du mois. |
| Prévision triennale | La programmation triennale des ressources relève du Budget-programme (`prog.ProgrammationRessource`) ; le module Recettes la met en regard des prévisions de l'année, par nature. |
| Mesure de la réalisation des recettes (C.2.2, C.2.9) | Le recouvrement (encaissement) ; le compte sur chiffres mesure aussi l'exécution par les **factures émises** (*Recettes › Factures émises*). |
| Saisie par poste (C.2.13) | Exigée pour les natures marquées « suivi par site » ; le compte tiers du site lie le poste à ses recettes. |
| Équilibre des reports (C.2.7) | Grandeurs A à I et contrôle H = I sur le Bilan et dans l'aperçu de clôture. Voir [19](19-reponses-metier.md). |

## Pas encore couvert

| Point | Dépend de |
|---|---|
| Recettes dans le tableau de bord général | Module Tableau de bord |

---

## Tests

`eProcessBudget.Tests/RecettesTests.cs` : formules (recouvrement, accroissement, bilan,
couverture de l'échéancier) ; prévisions (droit, nature de la famille de la ligne, site exigé
pour le péage, unicité, ligne de dépense refusée) ; prévision figée à l'ouverture, révision
motivée, échéancier plafonné ; encaissements (exercice ouvert, positionnement, grille
mensuelle, mois clos figé) ; indicateurs avec reports et accroissement à période comparable,
comparaison des sites sur trois exercices ; échéance en retard alertée une seule fois ; bilan
recettes / dépenses et versement des recettes à l'historique à la clôture.
