# 14 — Axes de dépenses

Le cinquième module (cahier fonctionnel § 3.5 à 3.8, technique § 4.5) : Entretien routier,
Investissements, Fonctionnement et Appuis. Les quatre axes **réutilisent le cycle commun**
([13](13-execution.md)) : leurs dotations, ajustements, engagements, paiements, reports et
alertes restent des pièces du cycle, saisies avec ses droits et ses règles. Le module ajoute
seulement ce qui est propre à chaque axe : projets, contrats, avancement physique,
inventaire, ainsi que les vues de synthèse.

| Droit | Permet |
|---|---|
| `EntretienLire` / `EntretienSaisir` | Consulter / saisir sur l'axe Entretien routier (opérations, contrats, avancement physique) |
| `InvestissementLire` / `InvestissementSaisir` | Consulter / saisir sur l'axe Investissements (projets, contrats, inventaire) |
| `FonctionnementLire` / `FonctionnementSaisir` | Consulter l'axe Fonctionnement (pas d'objet propre à saisir : tout passe par les dotations) |
| `AppuisLire` / `AppuisSaisir` | Consulter / saisir sur l'axe Appuis (projets d'appui, conventions) |

Le droit de saisie d'un axe donne aussi sa lecture (`AxeDepense.Lecture`). Comme pour le
cycle commun, toute saisie exige en plus une **habilitation de saisie active** couvrant l'axe,
le service ou le site touchés (`IControlePerimetre`).

---

## Les écrans

| Menu | Route | Contenu |
|---|---|---|
| Entretien routier | `/axes/entretien` | Chiffres clés de l'axe ; kilométrage prévu / entretenu par type de travaux ; opérations |
| Contrats d'entretien | `/axes/entretien/contrats` | Contrats de travaux et de prestations ; engagé, payé, km réalisés / contractuels |
| Avancement physique | `/axes/entretien/avancement` | Relevés mensuels : km entretenus, taux d'avancement, par contrat et type de travaux |
| Investissements | `/axes/investissements` | Projets (source RI / RE) ; situation par site, service, rubrique, nature |
| Inventaire | `/axes/investissements/inventaire` | Biens par site, compte d'immobilisation, projet d'acquisition, état |
| Comparaison inter-sites | `/axes/investissements/sites` | Taux d'engagement et de consommation par site, valeur de l'inventaire |
| Fonctionnement | `/axes/fonctionnement` | Par activité (clés de répartition), site, service, nature ; lignes ; lignes proches du dépassement ; personnel et effectifs |
| Appuis | `/axes/appuis` | Par entité (hiérarchie, sites de la zone d'implantation, solde) ; projets d'appui ; par rubrique |
| Versements d'appuis | `/axes/appuis/versements` | Historique par entité (sous-entités comprises) et projet ; appuis en attente de versement |
| (fiche) | `/axes/projets/{id}` | Projet : identité, `CarteSituation` sur l'exercice, dotations, engagements, paiements, contrats, avancement ou immobilisations |

La fiche projet réutilise `CarteSituation` et `TableEngagements` du cycle commun ; un clic sur
une dotation, un engagement ou un paiement ouvre l'écran correspondant de l'exécution.

---

## Comment c'est construit

```
Core/Enums/CalculsAxes.cs   AxeDepense / AxesDepense (code de ref.Axe + droits), formules, libellés

Infrastructure/Services/Axes/
├── AccesAxes.cs             droits par axe, périmètre, identifiant de l'axe dans l'organisme
├── RequetesAxes.cs          montants par projet, km réalisés, exécution par contrat
├── ProjetService.cs         projets des axes Entretien, Investissements, Appuis ; fiche et exécution
├── ContratService.cs        contrats de travaux, prestations, fournitures, conventions d'appui
├── AvancementService.cs     avancement physique ; synthèse km par type de travaux
├── ImmobilisationService.cs inventaire par site
├── SyntheseAxeService.cs    regroupements, lignes proches du dépassement, personnel, appuis
└── AlerteAppuis.cs          appuis en attente ; TraitementAppuisNocturne (BackgroundService, 2 h 30)

Web/Features/Axes/
├── Commun/                  ListeProjets, DialogProjet, TableContrats, DialogContrat, TableauRegroupement,
│                            CartesAxe, TableDotations, TablePaiements, Barre, ChampDate...
├── Entretien/  Investissements/  Fonctionnement/  Appuis/
└── DetailProjet.razor       fiche projet, commune aux trois axes à projets
```

Un axe est reconnu par le **code** de son `ref.Axe` (`ENTRETIEN`, `INVESTISSEMENT`,
`FONCTIONNEMENT`, `APPUIS`), posé par le provisionnement. Renommer le libellé d'un axe ne
change rien ; changer son code le détache du module.

### Où se calculent les montants

Partout par `SituationService.Calculer` du cycle commun (pièces validées seulement,
réalisation = paiements), puis cumulés par projet, site, service, entité, rubrique, nature ou
activité. Aucune donnée n'est ressaisie : un axe n'est qu'une vue sur ses dotations.

**Par activité** (Fonctionnement) : chaque dotation est ventilée entre les activités de sa
ligne selon la clé de répartition de l'exercice (`prog.CleRepartition`) ; tous les montants
sont multipliés par la clé et arrondis au centime (`CalculsAxes.Repartir`). Une ligne sans clé
apparaît en « Non réparti ».

---

## Les règles

| Règle | Où |
|---|---|
| Un projet appartient à un axe (Entretien, Investissements ou Appuis) et n'en change pas ; le Fonctionnement se gère par activité, sans projet | `ProjetService` |
| Code de projet unique dans l'organisme, tous axes confondus | `ProjetService` (+ index unique) |
| Investissement : source de financement obligatoire (RI / RE) | `ProjetService` |
| Appui : entité bénéficiaire obligatoire, tiers de type « Entité bénéficiaire d'appuis » | `ProjetService` |
| Opération de travaux : type de travaux (pas une prestation de contrôle), type de voie, maître d'ouvrage de type « Maître d'ouvrage », km prévus ≥ 0 ; complément `bud.ProjetTravaux` de même identifiant | `ProjetService` |
| Un projet avec dotations ne se supprime pas (il s'achève ou s'annule) ; il ne s'annule pas tant qu'il porte des engagements actifs | `ProjetService` |
| La dotation d'un projet porte sur une ligne **du même axe** | `DotationService` |
| Un engagement d'une dotation de projet ne vise qu'un contrat **de ce projet** | `EngagementService` |
| Types de contrat par axe : Entretien → travaux, prestation ; Appuis → convention d'appui ; Investissements → travaux, prestation, fourniture / service | `LibellesAxes.TypesContrat` |
| Prestation : type de prestation obligatoire (liste `TYPE_PRESTATION`) ; contrat contrôlé facultatif, contrat de **travaux** de la même opération | `ContratService` |
| Titulaire : prestataire (travaux, prestation, fourniture) ; entité bénéficiaire (convention d'appui) | `ContratService` |
| Montant initial > 0, avenants ≥ 0 ; numéro unique ; km contractuels pour les travaux seulement | `ContratService` |
| Un contrat avec engagements, prestations qui le contrôlent ou avancement ne se supprime pas | `ContratService` |
| Engagé supérieur au montant du contrat (avenants compris) : **signalé**, non bloquant | `LigneContrat.EngageAuDela` |
| Avancement : exercice ouvert, mois non clos ; km ≥ 0, taux entre 0 et 100 % ; un relevé par opération, mois, contrat et type de travaux ; sans type, celui de l'opération | `AvancementService` |
| Immobilisation : site et compte d'**immobilisation** actifs, projet de l'axe Investissements, code inventaire unique | `ImmobilisationService` |
| Lignes proches du dépassement : taux d'engagement (y compris reports) ≥ un seuil applicable (ligne > axe > global) | `SyntheseAxeService.ProchesDepassementAsync` |

### Formules (§ 4.1)

| Indicateur | Formule | Où |
|---|---|---|
| Taux d'avancement kilométrique | km réalisés cumulés / km prévus ; « n.s. » sans km prévus | `CalculsAxes.TauxAvancementKm` |
| Solde des appuis | prévu (dotation totale) − versé (paiements) | `CalculsAxes.SoldeAppui` |
| Montant d'un contrat | initial + avenants | `CalculsAxes.MontantContrat` |
| Taux de consommation des AD | celui du cycle commun | `MontantsDotation.TauxConsommationAD` |

### Entité d'un appui

L'entité d'une dotation d'appui est la sienne (`Dotation.TiersId`), sinon le bénéficiaire de
son projet. L'historique des versements d'une entité comprend ses sous-entités
(`Tiers.TiersParentId`, C.2.8). Pour une collectivité, l'écran indique les sites (postes de
péage) de sa zone d'implantation, paramétrés dans *Paramétrage › Sites* (`ref.SiteTiers`).

### Appuis en attente de versement

Un engagement d'appui **validé** dont il reste à verser (montant − versé − rejets) figure dans
*Versements d'appuis › En attente*. Passé 30 jours après sa validation
(`CalculsAxes.DelaiAppuiEnAttenteJours`), le traitement nocturne (2 h 30, organismes actifs,
exercices ouverts) notifie les détenteurs de `PaiementSaisir` et `AppuisSaisir`, au plus une
fois par engagement et par mois (clé `Appui:{id}:EnAttente:{aaaamm}`).

### Personnel et effectifs (Fonctionnement)

Dépenses des lignes de l'axe dont la nature est « Personnel » (code `1` des données
initiales), mises en regard des effectifs programmés de l'année dans le cycle rattaché à
l'exercice (`prog.ProgrammationEmploi`) et des agents actifs du référentiel, par catégorie
d'emplois.

---

### Sections du budget voté (C.2.4)

Les axes livrés portent leur section par défaut : **Investissement** pour l'axe
Investissements ; **Fonctionnement** pour l'Entretien routier, le Fonctionnement et les
Appuis (C.2.4 : « les travaux d'entretien et les appuis sont imputés en fonctionnement »).
La section se modifie dans *Paramétrage › Nomenclature*, sur l'axe. Elle range les
chapitres des comptes sur chiffres par section (fonctionnement, investissement).

Le provisionnement n'ajoute que les axes manquants : un organisme créé avant cette
correction garde l'Entretien routier en section Investissement. Corrigez-le à la main
dans le paramétrage, ou en base :

```sql
UPDATE ref.Axe SET Section = 1, DateModification = SYSUTCDATETIME()  -- 1 = Fonctionnement
WHERE Code = N'ENTRETIEN' AND Section = 2 AND EstSupprime = 0;        -- 2 = Investissement
```

**Réponse du métier (C.2.4) :** la réhabilitation est un programme d'investissement
(sous-rubrique 233, rubrique 23). Elle se saisit donc comme **projet de réhabilitation** de
l'axe Investissements, avec son kilométrage et son avancement physique ; l'Entretien routier
garde les travaux courants, périodiques et manuels. Voir [19](19-reponses-metier.md).

## Écarts avec le cahier technique

| Point | Choix |
|---|---|
| Enveloppes par maître d'ouvrage, report négatif (C.2.5) | Non activés, comme prévu par le cahier (§ 1.4) : la table existe, sans écran. Le maître d'ouvrage d'une opération est saisi. |
| Taux de couverture des besoins d'entretien, de conformité, d'exécution physique (C.2.1) | Calculés selon la réponse du métier ; besoins évalués saisis sur la page Entretien routier. Voir [19](19-reponses-metier.md). |
| Kilométrage (C.2.11) | Saisi par opération, mois, contrat et type de travaux (quatre types, dont la réhabilitation en investissement) ; synthèse par type de travaux. |
| Collectivités (C.2.8) | Plusieurs collectivités par poste ; une collectivité n'est rattachée qu'à un seul poste. |
| Historique des paiements par activité (§ 3.7.2) | Les paiements sont ventilés par activité dans la vue *Par activité* ; le détail pièce par pièce relève du module Consommation (`prog.vw_ConsommationActivite`). |

## Pas encore couvert

| Point | Dépend de |
|---|---|
| Comparaisons pluriannuelles (N-1, N, N+1), variation et part dans le budget | Module Rapports / Consommation |
| Exports PDF / Excel des écrans | Module Rapports |

---

## Tests

`eProcessBudget.Tests/AxesTests.cs` : formules propres aux axes et ventilation par clé ;
projets (droits par axe, source obligatoire en investissement, bénéficiaire typé en appui,
complément travaux de même identifiant, code unique) ; cohérence dotation ↔ axe du projet
et engagement ↔ contrat du projet ; contrats de travaux et prestations de contrôle (même
opération, titulaire typé, exécution par contrat) ; avancement physique (exercice ouvert,
relevé unique, mois clos, taux kilométrique) ; Fonctionnement par activité selon les clés
60 / 40, personnel face aux effectifs, lignes proches du dépassement ; appuis par entité
(hiérarchie, sites, solde), versements d'une tutelle et de ses sous-entités, alerte d'attente
au-delà de 30 jours émise une fois par mois ; inventaire par site et comparaison inter-sites,
invisibles sans droit sur l'axe.
