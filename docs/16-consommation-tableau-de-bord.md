# 16 — Consommation budgétaire et tableau de bord

Le septième module (cahier fonctionnel § 3.10 et 3.11, technique § 4.7) : la vision consolidée
de tous les axes de dépenses, et le tableau de bord qui devient la page d'accueil.

| Droit | Permet |
|---|---|
| `ConsommationLire` | Écran *Pilotage › Consommation budgétaire* |
| `TableauDeBordLire` | Tableau de bord budgétaire sur la page d'accueil (sinon, l'accueil montre l'état du compte) |

Les deux droits sont **restreints au périmètre** (cahier technique § 2.6 et 4.7) : voir plus bas.

---

## Les écrans

| Écran | Route | Contenu |
|---|---|---|
| Tableau de bord | `/` | Budget global et taux, volets Travaux / Investissement / Fonctionnement / Appuis (jauges, écarts prévisions ↔ engagements), évolution mensuelle (courbes), top 5 des rubriques, répartitions budget-programme et budgétaire (anneaux), alertes de seuils, engagements / paiements / reliquats par ligne, AE / CP par programme et budget-programme triennal, avancement par site, indicateurs de performance, recettes ; indicateurs de l'entretien routier (conformité, couverture des besoins, exécution physique, C.2.1) |
| Consommation budgétaire | `/consommation` | Tableau consolidé tous axes (prévisions initiales / ajustées / réalisations, factures, rejets, reliquats, projection) et recettes en regard ; consolidations ; AE / CP programmés et exécutés ; dérapages ; évolution mensuelle, trimestrielle, semestrielle ou annuelle ; tableau à double entrée |

Les deux écrans partagent la barre de filtres `FiltresConsommation` (§ 3.11.3) : exercice,
**situation à fin de** période (mois, trimestre, semestre ; sans période, situation courante),
axe, programme, nature de dépense, source de financement, site, service.

---

## Comment c'est construit

```
Core/Enums/CalculsConsommation.cs   part écoulée, projection linéaire, dérapage ; libellés de période et de volet

Infrastructure/Services/Consommation/
├── Consolidation.cs         moteur partagé : sélection (filtres + périmètre), situation à date, part du programme,
│                            regroupements (axe, programme, action, activité, nature, rubrique, sous-rubrique,
│                            ligne, projet, site, service, source, entité)
├── AccesConsommation.cs     droit + périmètre de lecture
├── ConsommationService.cs   consolidé, regroupements, AE / CP, dérapages, évolution, tableau croisé
└── TableauDeBordService.cs  tous les widgets en un calcul, mis en cache 5 minutes

Web/Features/Consommation/
├── Commun/                  FiltresConsommation, GraphiqueCourbes, GraphiqueRepartition (MudChart)
├── Index.razor              écran Consommation
└── TableauDeBordBudgetaire.razor   inséré dans Features/Accueil/Index.razor
```

Les montants viennent toujours de `SituationService.Calculer` (cycle commun), qui accepte
désormais une **date de situation** : seules comptent alors les pièces datées au plus tard ce
jour-là (ajustements, engagements, paiements, rejets) ; les reports, reçus à l'ouverture,
comptent toujours. Le regroupement des écrans d'axes (module 14) s'appuie sur le même moteur.

---

## Les règles

| Règle | Où |
|---|---|
| **Périmètre de lecture** : une habilitation active (lecture ou saisie) restreint les données à ses axes, programmes, actions, sites et services ; une habilitation sur l'organisme entier ouvre tout ; **sans aucune habilitation, la lecture suit les seuls droits du profil** (tout l'organisme) | `ControlePerimetre.PerimetreLectureAsync`, `Consolidation.Source` |
| Une dotation est dans le périmètre si son axe, son site ou son service y figure, ou si sa ligne est répartie sur un programme ou une action du périmètre | `Consolidation.Source` |
| Recettes (consolidé, tableau de bord) : visibles sur tout le périmètre ou avec l'axe Recettes | `ConsommationService` |
| Filtre programme : seule la **part** de chaque ligne qui revient au programme (somme de ses clés) est retenue, sur tous les montants | `Consolidation.CalculerAsync` |
| Projection de fin d'exercice : paiements à la date de situation / part écoulée de l'exercice (jours) ; le réalisé pour un exercice clôturé | `CalculsConsommation` |
| Dérapage : engagé au-delà de la dotation totale, ou paiements projetés au-delà | `CalculsConsommation.Derape` |
| AE / CP programmés : programmation financière de l'année de l'exercice, dans son cycle ; exécutés : engagé et payé ventilés par les clés de répartition | `ConsommationService.AECPAsync` |
| Évolution : engagé (hors encours reportés) et payé de chaque période ; prévision linéaire = dotation totale × part écoulée à la fin de la période | `ConsommationService.EvolutionAsync` |
| Tableau à double entrée : programme × rubrique, action × sous-rubrique, activité × ligne ; les lignes sont ventilées par les clés | `ConsommationService.CroiserAsync` |
| Tableau de bord : cache de 5 minutes par organisme, périmètre et filtre ; « Actualiser » force le recalcul | `TableauDeBordService` |
| Alertes du tableau de bord : dotations dont le taux d'engagement atteint un seuil applicable (ligne > axe > global), les 10 plus engagées | `TableauDeBordService` |

---

## Écarts avec le cahier technique

Choix validés le 11/10/2026, de même que la projection linéaire au prorata des jours écoulés
et la définition du dérapage.

| Point | Choix |
|---|---|
| Sources « vues SQL » (`bud.vw_SituationDotation`, `prog.vw_ConsommationActivite`...) | Les calculs passent par les mêmes requêtes EF que le cycle commun, avec les mêmes conventions : ils se testent sans SQL Server et acceptent une date de situation. Les vues restent disponibles pour les états. |
| Lecture sans habilitation | Tout l'organisme (droits du profil), pour que les profils de consultation (Direction, Administrateur) voient le tableau de bord sans habilitation. Pour une lecture strictement limitée aux habilitations, le choix tient en une ligne de `PerimetreLectureAsync`. |
| Factures « émises » et « recouvrées » (§ 3.10.2) | Lues côté recettes : prévu, encaissé (recouvré), reste à recouvrer. |
| Rapport de synthèse mensuel, trimestriel et annuel | Onglet *Évolution* ; l'export relève du module Rapports. |

## Pas encore couvert

| Point | Dépend de |
|---|---|
| Exports PDF / Excel des vues consolidées | Module Rapports |
| Comparaisons pluriannuelles (N-1 / N) en dépenses | Historique d'exécution et module Rapports |

---

## Tests

`eProcessBudget.Tests/ConsommationTests.cs` : part écoulée, projection, dérapage ; tableau
consolidé avec factures, situation à fin mars (paiement de mai exclu) et projection, part du
programme filtré, ventilation par activité et programme ; périmètre de lecture (axe seul, sans
habilitation, sans droit) ; AE / CP programmés et exécutés, évolution mensuelle et trimestrielle ;
dérapage projeté et tableaux croisés ; tableau de bord (volets, alerte de seuil, top rubriques,
triennal, performance) et son cache.
