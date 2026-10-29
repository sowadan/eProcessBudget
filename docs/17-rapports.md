# 17 — Rapports et exportation

Le huitième module (cahier fonctionnel § 3.12, technique § 2.10 et 4.8) : les états de gestion
budgétaire et les documents du budget-programme, exportés en Excel ou en PDF, ou ouverts dans
un onglet pour l'impression.

| Droit | Permet |
|---|---|
| `RapportExporter` | Écran *Pilotage › Rapports et exports* ; produire un état, **à condition** de pouvoir lire ses données (droit de lecture de l'état : consommation, axe, recettes, budget-programme, audit) |

Chaque export est **journalisé** (action `EXPORT`, module `Rapports`, avec le format et les
filtres).

---

## Les états

| Famille | Code | État | Paramètres |
|---|---|---|---|
| Gestion | `SITUATION_AXE` | Situation budgétaire par axe (rubriques, lignes) | exercice, axe, période (fac.) |
| Gestion | `SYNTHESE_GLOBALE` | Synthèse globale tous axes, recettes en regard | exercice, période (fac.) |
| Gestion | `CONSOMMATION_RUBRIQUE_COMPTE` | Consommation par rubrique et par compte comptable | exercice, axe (fac.), période (fac.) |
| Gestion | `AVANCEMENT_TRAVAUX` | Avancement des travaux routiers (km, exécution) | exercice |
| Gestion | `INVESTISSEMENTS_SITES` | Investissements par site et inventaire | exercice |
| Gestion | `APPUIS` | Appuis accordés et versés, appuis en attente | exercice |
| Gestion | `BILAN` | Bilan recettes / dépenses | exercice |
| Gestion | `PROJECTION` | Projection budgétaire et dérapages | exercice, période (fac.) |
| Gestion | `AUDIT` | Journal des modifications | du / au |
| Budget-programme | `DPPD` | DPPD : coût par programme, action, nature ; AE et CP de l'année de référence et des trois années ; total triennal | cycle |
| Budget-programme | `RESSOURCES` | Ressources de financement, équilibre ressources / emplois | cycle |
| Budget-programme | `INDICATEURS` | Indicateurs principaux et cibles | cycle, programme (fac.) |
| Budget-programme | `CADRE_LOGIQUE` | Cadre logique par programme | cycle, programme (fac.) |
| Budget-programme | `EMPLOIS` | Emplois par catégorie, global et par programme ; crédits de personnel | cycle |
| Budget-programme | `EXECUTION_TROIS_EXERCICES` | Exécution des trois derniers exercices par nature | exercice |
| Budget-programme | `RAPPORT_TRIMESTRIEL` | Rapport financier trimestriel | exercice, trimestre |
| Budget-programme | `RAPPORT_SEMESTRIEL` | Rapport semestriel d'exécution des programmes | exercice, semestre |
| Budget-programme | `RAPPORT_ANNUEL_PERFORMANCE` | Rapport annuel de performance (coûts, indicateurs, analyses) | exercice |

L'écran ne propose que les états que l'utilisateur peut produire.

---

## Comment c'est construit

```
Infrastructure/Services/Rapports/
├── ModeleRapport.cs        modèle d'état : sections (chiffres clés, texte, tableau), colonnes typées, lignes à niveaux
├── CatalogueRapports.cs    les 18 états : famille, titre, paramètres demandés, droits de lecture
├── GenerateurRapports.cs   un générateur par état, à partir des services des modules
├── RenduExcel.cs           ClosedXML : feuille Synthèse + une feuille par tableau, formats, totaux par formule
├── RenduPdf.cs             QuestPDF : A4 portrait / paysage, en-tête et pied de page institutionnels, pagination
└── RapportService.cs       droits, contrôle des paramètres, filtres en clair, identité de l'organisme, journalisation ;
                            DepotRapports : fichier gardé 10 minutes pour l'ouvrir dans un onglet

Web/Features/Rapports/
├── Index.razor                     catalogue, paramètres, Excel / PDF / préparer l'impression
└── PointsTerminaisonRapports.cs    GET /rapports/fichier/{jeton} : PDF en ligne, réservé à son auteur
```

**Un état ne recalcule rien** : il présente ce que montrent les écrans, par les mêmes services
(consommation, axes, recettes, budget-programme, audit), avec leurs formules, leurs droits et le
périmètre de lecture de l'utilisateur. Le modèle est produit une fois, puis rendu dans le format
demandé (cahier technique § 2.10).

### En-tête et pied de page

Repris de *Paramétrage › Organisme* : raison sociale et sigle, adresse et contacts, **logo**,
**en-tête** et **pied de page des rapports**, devise de référence. Chaque export affiche les
filtres appliqués en clair (cahier fonctionnel § 4.2) et sa date d'édition ; le PDF est paginé.

### Excel

Une feuille *Synthèse* (identité, titre, filtres, chiffres clés, textes, renvois), puis une feuille
par tableau : en-têtes mis en forme, volets figés, montants au format `#,##0.00`, taux en `%`,
« n.s. » pour un taux sans dénominateur. Le total d'un tableau à plat est une **formule** `SOMME`
(il suit les corrections faites dans le classeur) ; un tableau à sous-totaux garde des valeurs.
Les codes et numéros restent du texte.

### Impression

« Préparer l'impression » produit le PDF et donne un lien à ouvrir dans un nouvel onglet, où le
navigateur l'imprime. Le lien est valable 10 minutes et ne sert qu'à l'utilisateur qui l'a produit.

---

## Licence QuestPDF : à vérifier

QuestPDF est gratuit sous licence **Community** pour une entité dont le chiffre d'affaires annuel
brut est inférieur à 1 M$ US (et pour certains usages non lucratifs) ; au-delà, une licence
Professional ou Enterprise est requise. La licence se déclare dans `appsettings.json` :

```json
"Rapports": { "LicenceQuestPdf": "Community" }
```

Valeurs admises : `Community`, `Professional`, `Enterprise` ; une autre valeur empêche le
démarrage. **L'organisme exploitant vérifie son éligibilité avant la mise en production**
(cahier technique § 2.1). iTextSharp, cité par le cahier fonctionnel, est écarté (licence AGPL).

---

## Écarts avec le cahier technique

Choix validés le 11/10/2026. Reste ouverte la vérification de la licence QuestPDF (ci-dessus).

| Point | Choix |
|---|---|
| « Exports volumineux générés en tâche de fond » | Génération à la demande, dans la requête : les états restent de taille modeste (journal d'audit plafonné à 5 000 lignes). À déplacer dans une file si les volumes l'exigent. |
| Un droit par famille d'états | Un seul droit `RapportExporter`, combiné au droit de lecture des données de chaque état. |
| Impression directe | PDF ouvert dans un onglet du navigateur, plutôt qu'une mise en page HTML dédiée : le document imprimé est celui qui s'exporte. |
| États de fin d'exercice (C.2.7, C.2.9) | Quatre états selon les tableaux du métier : comptes sur chiffres recettes et dépenses, équilibre de trésorerie, état de présentation N / N-1 (commentaires par rubrique sur les variations significatives). Voir [19](19-reponses-metier.md). |

---

## Tests

`eProcessBudget.Tests/RapportsTests.cs` : rendu Excel (feuilles, en-tête, filtres, « n.s. »,
total par formule, format monétaire) ; rendu PDF et formats français ; catalogue filtré par les
droits, contrôle des paramètres (exercice, axe, type de période, dates, cycle) ; **chacun des 18
états produit en Excel et en PDF** sur un jeu de données réel, et journalisé ; montants de la
synthèse globale et du DPPD retrouvés dans le classeur.
