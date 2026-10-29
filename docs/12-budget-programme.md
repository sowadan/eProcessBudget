# 12 — Budget-programme et performance

Le troisième module (cahier technique § 4.3) : programmes, actions, activités et
indicateurs ; cibles et réalisations ; cadre logique ; programmation triennale AE / CP,
ressources et emplois ; clés de répartition et temps des agents ; analyses qualitatives.

Droits :

| Droit | Permet |
|---|---|
| `BudgetProgrammeLire` | Consulter tous les écrans du module |
| `BudgetProgrammeDefinir` | Gérer programmes, actions, activités, indicateurs, cibles, cadre logique, analyses |
| `ProgrammationSaisir` | Saisir la programmation AE / CP, les ressources, les emplois, les clés et les temps |
| `IndicateurSaisirRealisation` | Saisir et valider les réalisations des indicateurs, saisir les analyses |

Chacun de ces quatre droits donne aussi la lecture (`CatalogueReferentiels.LectureBudgetProgramme`).

---

## Les écrans

| Menu | Route | Contenu |
|---|---|---|
| Programmes | `/budget-programme` | Arborescence programme › action › activité ; responsables, structures conjointes (principale marquée d'une étoile) |
| (référentiels) | `/budget-programme/programmes`, `actions`, `activites`, `indicateurs` | Listes, formulaires, export / import Excel (ordre : programmes, actions, activités, indicateurs) |
| Indicateurs | `/budget-programme/indicateurs` | Indicateurs, valeur de référence ; action **Cibles** par année du cycle |
| Tableau des cibles | `/budget-programme/tableau-indicateurs` | Cibles de chaque indicateur sur les années du cycle choisi |
| Cadre logique | `/budget-programme/cadre-logique` | Objectifs, résultats, indicateurs objectivement vérifiables, sources, hypothèses ; risques par élément ; anomalies signalées |
| Programmation | `/budget-programme/programmation` | AE / CP par ligne et par année, ressources par nature, emplois par catégorie ; récapitulatif, équilibre, effectifs et charges de personnel |
| Répartitions | `/budget-programme/repartitions` | Clés de répartition des lignes entre activités, temps des agents, ventilation des charges |
| Performance | `/budget-programme/performance` | Réalisations de l'exercice, taux d'atteinte, justification, validation / réouverture ; synthèse |
| Analyses | `/budget-programme/analyses` | Contraintes, priorités, défis, recommandations, commentaires de rubrique |

---

## Comment c'est construit

```
Core/Enums/CalculsBudgetProgramme.cs   calculs purs : taux d'atteinte, ventilation, total triennal ; libellés

Infrastructure/Services/BudgetProgramme/
├── AccesBudgetProgramme.cs     lecteur / acteur, périmètre, cycle modifiable, exercice saisissable
├── ProgrammeService.cs         arborescence, responsables, structures conjointes
├── IndicateurService.cs        cibles, tableau, réalisations, validation, synthèse
├── CadreLogiqueService.cs      éléments, risques, anomalies (utilisées à la validation du cycle)
├── ProgrammationService.cs     AE / CP, ressources, emplois, récapitulatif, équilibre
├── ReconductionCycle.cs        copie d'un cycle vers le suivant
├── RepartitionService.cs       clés, temps, ventilation
└── AnalyseService.cs

Infrastructure/Services/Parametrage/CatalogueReferentiels.BudgetProgramme.cs
                                 descripteurs des 4 référentiels (même mécanique que le paramétrage)
Infrastructure/Services/Plateforme/ControlePerimetre.cs
                                 IControlePerimetre : la saisie est-elle dans le périmètre de l'utilisateur ?

Web/Features/BudgetProgramme/
├── Formulaires/, Referentiels/  les 4 référentiels
├── Commun/                      SelecteurCycle, SelecteurExercice
└── pages et dialogues des écrans ci-dessus
```

Les programmes, actions, activités et indicateurs sont des référentiels comme les
autres : `IReferentielService<Programme>`… Leur descripteur fixe les droits de lecture et
de gestion (`DroitsLecture`, `DroitsGestion`) et le module d'audit.

---

## Les règles

| Règle | Où |
|---|---|
| Programmation (AE / CP, ressources, emplois), cibles et cadre logique modifiables seulement sur un cycle **Brouillon** | `AccesBudgetProgramme.CycleModifiableAsync` |
| Réalisations et analyses : exercice **Ouvert** ; clés et temps : Ouvert **ou En préparation** | `ExerciceSaisissableAsync` |
| CP cumulés ≤ AE cumulés, année après année | `ProgrammationService.ControlerAECP` |
| Clés d'une ligne : exactement 100 % (ou aucune clé) | `RepartitionService.ControlerCles` |
| Temps d'un agent : 100 % au plus | `RepartitionService` |
| Taux d'atteinte : réalisé / cible à la hausse, cible / réalisé à la baisse, × 100, arrondi à 2 décimales ; « n.s. » si le dénominateur est nul | `CalculsBudgetProgramme.TauxAtteinte` |
| Cible non atteinte : **justification obligatoire** ; une réalisation validée n'est plus modifiable (réouverture tracée) | `IndicateurService` |
| Ventilation : arrondi à 2 décimales, le reste d'arrondi va à la plus grosse part (le total tombe juste) | `CalculsBudgetProgramme.Ventiler` |
| Validation d'un cycle refusée tant que le cadre logique présente des anomalies (en plus de la règle sur les cibles) | `CycleProgrammationService` → `CadreLogiqueService.AnomaliesAsync` |
| Nouveau cycle avec un cycle précédent : programmation, ressources, emplois, cibles, cadre logique et risques sont reconduits | `ReconductionCycle.CopierAsync` |

### Périmètre de saisie

Une saisie n'est admise que si l'utilisateur détient une habilitation de saisie **active**
qui la couvre : organisme entier, ou le programme, l'action, l'axe, la structure ou le
site concernés (`IControlePerimetre.VerifierSaisieAsync`).

| Saisie | Ce qui est contrôlé |
|---|---|
| Réalisation d'un indicateur | Programme, action ou structure de l'indicateur |
| AE / CP | Programme / action de la ligne de programmation |
| Ressources | Organisme entier |
| Emplois | Programme |
| Clés d'une ligne | Axe de la ligne, ou chaque activité visée |
| Temps d'un agent | Structure de l'agent, ou chaque action visée |
| Analyse | Programme, s'il est précisé ; sinon organisme entier |

La **lecture** n'est pas filtrée par le périmètre : quiconque a un droit du module voit
tout l'organisme.

---

## Pas encore couvert

| Point | Dépend de |
|---|---|
| Exports PDF / Excel du cadre logique et des tableaux de performance | Module Rapports |
| Historique d'exécution (§ 3.3.8) et taux d'exécution physique | Module Exécution |

---

## Tests

`eProcessBudget.Tests/BudgetProgrammeTests.cs` : taux d'atteinte (hausse, baisse,
cible nulle), ventilation exacte au centime, CP cumulés ≤ AE cumulés, clés à 100 %,
clés refusées hors périmètre puis admises, justification exigée pour une cible manquée
et validation, reconduction d'un cycle (projections et cibles), ventilation depuis le
temps des agents.
