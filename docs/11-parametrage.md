# 11 — Paramétrage

Le deuxième module (cahier technique § 4.2) : paramètres de l'organisme, exercices et
cycles, nomenclature budgétaire, et les vingt référentiels sur lesquels s'appuient tous
les autres modules. Aucune saisie budgétaire n'est possible sur un élément non paramétré.

Droits : `ParametrageLire` (consulter) et `ParametrageGerer` (modifier). Tout se passe
dans l'organisme de l'utilisateur connecté.

---

## Les écrans

| Menu | Route | Contenu |
|---|---|---|
| Vue d'ensemble | `/parametrage` | Accès à tous les paramètres et référentiels, groupés comme dans le cahier |
| Organisme | `/parametrage/organisme` | Identité, logo, devise de référence, session, tentatives, pagination, rétention, en-tête et pied des rapports ; « Compléter les données initiales » |
| Exercices et cycles | `/parametrage/exercices` | Exercices (création avec 19 périodes, ouverture, clôture des périodes) et cycles triennaux (validation, reconduction, archivage) |
| Nomenclature | `/parametrage/nomenclature` | Arborescence axe › rubrique › sous-rubrique › ligne, comptes d'imputation des lignes |
| Import Excel | `/parametrage/import` | Modèle et import de chaque référentiel, dans l'ordre des dépendances |
| (vue d'ensemble) | `/parametrage/<clé>` | Une page par référentiel : `structures`, `sites`, `tiers`, `plan-comptable`… |

---

## Comment c'est construit

Vingt référentiels partagent le même comportement : liste paginée et filtrée côté base,
création, modification, activation, suppression logique, unicité de la clé dans
l'organisme, audit, export et import Excel. Ce comportement est écrit **une fois** :

```
Infrastructure/Services/Parametrage/
├── ReferentielService.cs        IReferentielService<T> : tout le comportement commun
├── CatalogueReferentiels.cs     ★ un descripteur par référentiel : ce qui le distingue
├── DescripteurReferentiel.cs    clé métier, champs modifiables, règles, colonnes Excel
├── VerificateurUtilisation.cs   « est-il utilisé ? », déduit du modèle EF Core
├── ExcelReferentiel.cs          lecture / écriture ClosedXML
└── ExerciceService, CycleProgrammationService, ParametresOrganismeService,
    SeuilAlerteService, TauxChangeService, NomenclatureService

Web/Features/Parametrage/
├── Commun/PageReferentiel.razor  l'écran type, générique
├── Formulaires/                  un formulaire par référentiel (réutilisé par la nomenclature)
└── Referentiels/                 une page par référentiel : ses colonnes, rien d'autre
```

On injecte `IReferentielService<Structure>`, `IReferentielService<Axe>`… : un seul
enregistrement générique dans `ServiceCollectionExtensions`.

### Ajouter un référentiel

1. L'entité dans `Core/Entities/Referentiels.cs` (héritant d'`EntiteOrganisme`, avec
   `IActivable` si elle a un statut), sa configuration, sa migration.
2. Son descripteur dans `CatalogueReferentiels` : clé métier, `Copier` (champs
   modifiables), règles, colonnes Excel.
3. `Formulaires/Formulaire<Type>.razor` (hérite de `FormulaireBase<T>`).
4. `Referentiels/<Pluriel>.razor` : `@page`, en-têtes et cellules de la liste.

La page apparaît d'elle-même dans la vue d'ensemble et dans l'import Excel.

---

## Les règles communes

| Règle | Où |
|---|---|
| Clé métier unique **dans l'organisme**, lignes supprimées exclues (un code supprimé se réutilise) | `ReferentielService.ValiderAsync` + index uniques filtrés |
| Références vers des éléments du même organisme ; actives à la création | `CatalogueReferentiels.ReferenceAsync` (filtre global d'organisme) |
| Pas de cycle dans une hiérarchie (structures, comptes, tiers, natures de recettes) | `CatalogueReferentiels.SansCycleAsync` |
| **Désactivation refusée** tant que des éléments **actifs** en dépendent (une rubrique qui a des sous-rubriques actives) | `ChangerEtatAsync` |
| **Suppression refusée** tant qu'un enregistrement, actif ou non, y est rattaché (règle § 3.4) ; proposer la désactivation | `SupprimerAsync` |
| Concurrence : une modification faite entre-temps par quelqu'un d'autre est refusée | `RowVersion` |

« Est-il utilisé ? » ne repose sur aucune liste écrite à la main : `VerificateurUtilisation`
parcourt toutes les clés étrangères qui pointent vers la table dans le modèle EF Core, y
compris celles des modules à venir (dotations, engagements…). Chaque test est un `EXISTS`
paramétré, dans l'organisme courant. Les tables de liaison « possédées » (comptes d'une
ligne, collectivités d'un site) sont supprimées avec l'élément.

### Règles propres

| Référentiel | Règle |
|---|---|
| Ligne budgétaire | Nature de dépense **ou** de recette selon le sens (CHECK en base) ; le sens suit le type de l'axe ; au moins un compte d'imputation, dont un principal (signalé dans la nomenclature tant qu'il manque) |
| Plan comptable | Classe 1 à 9, déduite du premier chiffre si elle n'est pas saisie |
| Type de travaux | Une prestation de contrôle désigne les travaux qu'elle contrôle |
| Tiers | Compte général de type « général », compte auxiliaire de type « auxiliaire » |
| Devise | Code ISO 4217 à trois lettres ; la devise de référence se choisit dans « Organisme » et n'a pas de taux |
| Exercice | Unique par année ; 19 périodes générées (12 mois, 4 trimestres, 2 semestres, l'année) ; périodes closes ou rouvertes sur un exercice **ouvert** ; suppression seulement en préparation et s'il n'est pas utilisé ; l'ouverture **fige la prévision initiale** des dotations ([13](13-execution.md)) |
| Cycle | N-1, N, N+2 calculés depuis l'année de base ; **validation refusée** tant qu'un indicateur n'a pas sa valeur de référence et une cible pour chaque année du cycle (règle § 3.4) |
| Logo | PNG ou JPEG, 512 Ko au plus, type vérifié sur le contenu (pas de SVG, qui peut contenir du script) |

La **clôture** d'un exercice (reliquats, encours, reports) relève du module Exécution
(droit `ExerciceCloturer`, écran *Exécution budgétaire › Clôture*, voir [13](13-execution.md)).

---

## Import Excel

Pour chaque référentiel, le bouton **Exporter** produit le modèle : la ligne d'en-têtes
(`*` = obligatoire), les éléments existants, et une feuille « Aide » (valeurs admises).
On le complète, on le réimporte.

| Point | Comportement |
|---|---|
| Rapprochement | Par la première colonne (code) : connu → mise à jour, nouveau → création. Listes de valeurs : par liste et code. |
| Références | Saisies par leur code (l'axe d'une rubrique : `FONCTIONNEMENT`). D'où l'ordre proposé dans la page Import. |
| Erreurs | **Tout ou rien** : une seule ligne en erreur et rien n'est enregistré. Le rapport donne chaque anomalie avec sa ligne Excel et sa colonne. |
| Limites | `.xlsx` de 5 Mo et 5 000 lignes au plus. |
| Valeurs | Oui / Non, libellés des listes (ou leur numéro), dates `jj/mm/aaaa`. Toutes les cellules exportées sont au format texte (un code « 01 » reste « 01 »). |

---

## Données initiales (§ 3.7)

Créées avec chaque organisme, et complétables à tout moment par **Organisme › Compléter
les données initiales** (seuls les éléments absents, par code, sont ajoutés ; rien
d'existant n'est modifié) :

paramètres (session 30 min, 5 tentatives, pagination 20, rétention 12 mois), devise XOF,
seuils 80 / 90 / 100 %, 5 axes, 5 natures de dépense, sources RI / RE, 5 unités de
mesure, 8 listes de valeurs (types de structure, de voie, de prestation, catégories
d'entités, sources de vérification, catégories de risques, types de pénalité,
périodicités), 5 structures (annexe B.3), 7 catégories d'emplois, 7 risques (annexe B.2),
8 types de travaux et leurs 8 prestations de contrôle, natures de recettes en deux
niveaux (recettes propres, taxes affectées, ressources additionnelles et externes,
reports).

> Les programmes, actions et indicateurs de l'annexe B.1 relèvent du module
> Budget-programme.

---

## Tests

`eProcessBudget.Tests/ParametrageTests.cs` couvre les tests clés du § 4.2 : unicité des
codes par organisme, code supprimé réutilisable, désactivation refusée si l'élément est
utilisé, suppression avec liaisons possédées, cohérence sens / axe, génération des
périodes (année bissextile, contiguïté des mois), cycle de vie d'un exercice, import
Excel (rapport ligne par ligne, tout ou rien, colonne obligatoire absente, export
réimportable), droits, et idempotence des données initiales.
