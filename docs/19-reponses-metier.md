# 19 — Réponses du métier (annexe C.2)

Les réponses du métier aux questions de l'annexe C.2 du cahier fonctionnel sont dans
`cdc/Répense aux Question- eProcessBudget.docx`. Cette page indique ce que chaque réponse a
changé dans l'application, et où.

Migration : `ReponsesMetier` (à appliquer sur la base, `dotnet ef database update`). Elle crée
`bud.BesoinEntretien` et `rec.EmissionRecette`, ajoute `ref.Site.CompteTiersId` et
`rec.ReportRessource.MontantPrevu`, et rend `ref.SiteTiers.TiersId` unique.

> **Avant d'appliquer la migration**, vérifiez qu'aucune collectivité n'est rattachée à deux
> postes. Sinon, l'index unique échoue :
>
> ```sql
> SELECT TiersId, COUNT(*) FROM ref.SiteTiers GROUP BY TiersId HAVING COUNT(*) > 1;
> ```

---

## Synthèse

| # | Réponse | Ce qui change | Où |
|---|---|---|---|
| C.2.1 | Conformité = AD / prévisions affectées ; couverture = financement de l'entretien / besoins évalués ; exécution physique = **crédits de paiement / AD** | Saisie des besoins évalués (globaux ou par type de travaux) ; trois indicateurs sur la page Entretien routier, au tableau de bord et dans l'état « Avancement des travaux » | `BesoinEntretienService`, `IndicateursEntretien`, `CarteIndicateursEntretien` |
| C.2.2 | « La dépense, c'est le décaissement ; tout décaissement se rapporte à une facture reçue » | Un paiement porte sur une facture de l'engagement (ou de l'engagement d'origine d'un encours reporté) et n'en dépasse pas le reste ; les versements d'appuis en sont dispensés. Les factures reçues forment la « prise en charge » du compte sur chiffres | `PaiementService.EnregistrerAsync`, `ChaineEngagementsAsync` |
| C.2.4 | La réhabilitation est un programme d'investissement (sous-rubrique 233, rubrique 23) | Un projet de l'axe Investissements peut être un **projet de réhabilitation** : type de travaux fixé, kilométrage, avancement physique. Le type « réhabilitation » est refusé en Entretien routier | `ProjetService`, `AvancementService`, `DialogProjet` |
| C.2.5 | Le maître d'ouvrage (ministères) engage : on parle d'AD ; report et dépassement sont distincts | Rien à développer : pas d'enveloppe ni de report négatif | — |
| C.2.6 | Formule de l'encours confirmée | Inchangé | `MontantsDotation.EncoursAReporter` |
| C.2.7 | Le report est la part des AD qui n'ont pas reçu leur crédit de paiement | État « Équilibre de trésorerie et reports » (A à I, contrôle H = I) ; affiché sur le Bilan et dans l'aperçu de clôture | `CompteSurChiffresService.EquilibreAsync`, `CarteEquilibreTresorerie` |
| C.2.8 | Plusieurs collectivités peuvent être rattachées à un poste, mais pas l'inverse | Une collectivité n'est rattachée qu'à un seul poste (contrôle et index unique) | `NomenclatureService.DefinirCollectivitesSiteAsync` |
| C.2.9 | Comptes sur chiffres recettes / dépenses, tableau comparatif des deux exercices, commentaires sur les variations significatives | Saisie des **factures émises** ; quatre états : comptes sur chiffres recettes et dépenses, équilibre de trésorerie, état de présentation N / N-1 | `EmissionRecetteService`, `CompteSurChiffresService`, `GenerateurRapports` |
| C.2.10 | « Je te laisse prendre la décision » | Décision : **cinq exercices** avant le premier exercice géré, repris par import Excel (modèle, contrôle tout ou rien, remplacement des années importées) | `HistoriqueService`, *Paramétrage › Reprise de l'historique* |
| C.2.11 | Quatre types de travaux : courants, périodiques, manuels (cantonnage), projets de réhabilitation (investissement) | Données initiales remplacées ; les anciens types non utilisés sont désactivés | `ProvisionnementOrganisme`, `CodesTypeTravaux` |
| C.2.13 | Recettes saisies par poste : un compte tiers lie chaque poste à son produit | Compte tiers (auxiliaire) sur le site | `Site.CompteTiersId`, `FormulaireSite` |

---

## Indicateurs de l'entretien (C.2.1)

Le **financement de l'entretien** est la situation des dotations de l'axe Entretien routier
et des projets de réhabilitation de l'axe Investissements.

| Indicateur | Formule |
|---|---|
| Taux de conformité | AD (y compris encours reportés) / prévisions affectées (dotation totale) |
| Couverture des besoins | prévisions affectées / besoins évalués ; « n.s. » sans besoins saisis |
| Exécution physique | crédits de paiement (payés) / AD |

Les besoins se saisissent sur la page *Entretien routier*, par exercice non clôturé : soit un
besoin global, soit un besoin par type de travaux, jamais les deux (pas de double compte).

## Projets de réhabilitation (C.2.4, C.2.11)

| Type de travaux | Axe |
|---|---|
| `COURANTS` — travaux courants d'entretien | Entretien routier |
| `PERIODIQUES` — travaux périodiques d'entretien | Entretien routier |
| `CANTONNAGE` — travaux manuels d'entretien | Entretien routier |
| `REHABILITATION` — projets de réhabilitation | Investissements |

Chaque type a sa prestation de contrôle (`CTRL_…`). Les types de voie restent une liste de
valeurs distincte. Dans l'axe Investissements, l'interrupteur « Projet de réhabilitation »
du formulaire de projet fixe le type et ouvre les champs de travaux (maître d'ouvrage,
localisation, kilométrage). L'avancement physique (*Entretien routier › Avancement*) porte
sur les opérations d'entretien et sur les réhabilitations ; le droit de saisie est celui de
l'axe de l'opération.

## Comptes sur chiffres (C.2.9)

**Recettes**, par ligne budgétaire, regroupées par section (celle de l'axe) :

| Colonne | Calcul |
|---|---|
| Prévisions ajustées | prévision révisée, sinon initiale |
| Factures émises, % exéc. | Σ factures émises ; émis / prévision |
| Factures recouvrées, % rec. | Σ encaissements ; recouvré / émis |
| Reste à recouvrer, % RAR | émis − recouvré ; reste / émis |

Puis les reports de ressources (521, 409, 4495, 411…) : inscrit au budget
(`ReportRessource.MontantPrevu`), constaté = émis = recouvré.

**Dépenses**, par chapitre (code de rubrique, tous axes confondus), regroupées par section :

| Colonne | Calcul |
|---|---|
| Prévisions ajustées | prévision initiale ± ajustements |
| Prise en charge, % exéc. | factures reçues sur les AD de l'exercice ; prise en charge / prévision |
| Rejets ou pénalités | rejets des AD de l'exercice |
| Paiements, % pmt | paiements sur ces AD ; paiements / prise en charge |
| Reste à payer, % RAP | prise en charge − paiements − rejets ; reste / prise en charge |

Puis la ligne « reports d'engagements antérieurs » : reports, encours reportés, leurs rejets
et paiements. Pour mémoire : la part des travaux d'entretien routier (entretien et
réhabilitation).

Les factures émises se saisissent dans *Recettes › Factures émises* (exercice ouvert, mois
non clos, droit `EncaissementSaisir`).

## Équilibre de trésorerie (C.2.7)

| Grandeur | Calcul |
|---|---|
| A | total général des recettes recouvrées (reports de ressources compris) |
| B | paiements + rejets ou pénalités |
| C | A − B |
| D | reste à payer total : encours reporté sur l'exercice suivant |
| E | C − D |
| F | reste à recouvrer des taxes affectées |
| G | reste à recouvrer des autres recettes |
| H | E + F + G |
| I | total des recettes émises − total des dépenses prises en charge |

H = I par construction ; un écart signale une incohérence de saisie. Le test
`Equilibre_reproduit_le_tableau_2025_du_metier` reprend les chiffres 2025 transmis
(H = I = 2 730 613 311).

## État de présentation du budget (C.2.9)

Lignes de recettes et chapitres de dépenses de l'exercice et du précédent : prévision N-1,
réalisé N-1, prévision N, variation, variation en %, part dans le total. Une variation est
**significative** à partir de 10 % (`CalculsPresentation.SeuilVariationSignificative`), ou
quand une ligne apparaît. Le commentaire est celui de la rubrique (*Budget-programme ›
Analyses*, type « commentaire de rubrique ») ; à défaut, l'état signale qu'il est attendu.

## Reprise de l'historique (C.2.10)

Années admises : les cinq qui précèdent le premier exercice géré
(`HistoriqueService.AnneesReprises`). Le modèle Excel a une feuille « Historique » (année,
axe, rubrique, nature de dépense, nature de recette, site, prévision, montant autorisé,
réalisé, restant à payer, km entretenus) et une feuille « Codes ». Une ligne de dépense
porte un axe ; une ligne de recette porte une nature de recette. Si une seule ligne est en
erreur, rien n'est enregistré. Importer une année remplace ses lignes importées ; les
lignes calculées à la clôture ne sont jamais touchées.

---

## Tests

- `AxesTests` : réhabilitation en investissement, avancement multi-axes, besoins et indicateurs de l'entretien.
- `ExecutionTests` : paiement refusé sans facture, plafonné au reste de la facture.
- `RecettesTests` : factures émises, compte sur chiffres, équilibre H = I, état de présentation ; chiffres 2025 du métier.
- `ParametrageTests` : collectivité rattachée à un seul poste ; reprise de l'historique (années admises, tout ou rien, remplacement).
