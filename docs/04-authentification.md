# 04 — Plateforme et authentification

Le premier module métier (cahier technique § 4.1) : connexion, sessions, comptes,
organismes et habilitations. Ce document dit **comment ça marche** et **où intervenir**.

---

## Vue d'ensemble

```
Navigateur ──POST /compte/connexion──► Connexion.razor (rendu serveur)
                                          │  IAuthentificationService.ConnecterAsync
                                          │  (BCrypt, blocage, JournalConnexion)
                                          ▼
                                   cookie « epb.session »  (HttpOnly, Secure, SameSite=Strict)
                                          │  revendications : utilisateur, organisme, empreinte
                                          ▼
       circuit Blazor ──► IPrincipalCourant ──► IUserContextService ──► IDroitService
                              │
                              └──► FabriqueContexteOrganisme : chaque DbContext reçoit
                                   l'organisme de l'utilisateur (filtre global § 2.3)
```

| Brique | Fichier | Rôle |
|---|---|---|
| Configuration de l'hôte | `Web/Securite/ConfigurationSecurite.cs` | Cookie, accès authentifié par défaut, limitation de débit, en-têtes |
| Pages publiques | `Web/Features/Compte/` | Connexion, mot de passe oublié, réinitialisation (rendu serveur) |
| Déconnexion, signal d'activité | `Web/Securite/PointsTerminaisonCompte.cs` | `POST /compte/deconnexion`, `GET /compte/session` |
| Revalidation du circuit | `Web/Securite/EtatAuthentificationRevalide.cs` | Revérifie la session toutes les 60 s |
| Inactivité | `Web/wwwroot/js/session.js` | Déconnecte après la durée d'inactivité de l'organisme |
| Authentification | `Infrastructure/Services/Securite/AuthentificationService.cs` | Connexion, blocage, journal, réinitialisation, validité des sessions |
| Hachage | `Infrastructure/Services/Securite/HacheurMotDePasse.cs` | `IPasswordHasher` BCrypt (coût ≥ 12) et politique de mot de passe |
| Utilisateur connecté | `Infrastructure/Services/Securite/UserContextService.cs` | Lit l'identité du cookie, charge le compte |
| Cloisonnement | `Infrastructure/Data/FabriqueContexteOrganisme.cs` | Renseigne `OrganismeCourantId` sur chaque contexte |
| Organismes | `Infrastructure/Services/Plateforme/OrganismeService.cs` | Création + provisionnement, statut |
| Habilitations | `Infrastructure/Services/Plateforme/HabilitationService.cs` | Circuit de double validation |

### Pourquoi un cookie et pas un jeton JWT

Le circuit SignalR de Blazor Server s'ouvre avec les cookies du navigateur : aucun code
client n'est nécessaire pour transmettre l'identité. Un JWT obligerait à le stocker côté
navigateur (exposé au JavaScript) et à le passer à chaque connexion du hub.

### Pourquoi les pages de connexion ne sont pas interactives

Poser ou retirer un cookie exige une vraie requête HTTP. Un composant interactif ne parle
qu'au circuit SignalR, déjà ouvert. Les pages `/compte/connexion`, `/compte/mot-de-passe-oublie`
et `/compte/reinitialiser` portent donc `[ExcludeFromInteractiveRouting]` : `App.razor` les
rend côté serveur, en simples formulaires HTML.

---

## Premier démarrage : le compte d'amorçage

Sur une base sans utilisateur, le seed crée un **Administrateur plateforme** à partir de
la section `Amorcage` — à condition qu'un mot de passe soit fourni, **jamais** dans
`appsettings.json` :

```bash
cd eProcessBudget.Web
dotnet user-secrets set "Amorcage:MotDePasse" "Un#MotDePasse2026"
```

En production : variable d'environnement `Amorcage__MotDePasse`, le temps du premier
démarrage (avec `Persistance__MigrerAuDemarrage=true`), puis retirez-la.

> **Base créée avant ce module.** Si le compte `Amorcage:Login` existe déjà **sans mot de
> passe** (cas du compte `admin` créé par l'ancien seed), il reçoit le mot de passe fourni
> et, s'il n'a pas d'organisme, devient Administrateur plateforme. Le seed ne touche jamais
> un compte qui a déjà un mot de passe.

Ensuite, avec ce compte :

1. *Plateforme › Organismes* : créez l'organisme (ses données initiales sont créées avec lui) ;
2. *Administration › Utilisateurs* : créez son administrateur, rattaché à l'organisme,
   avec le profil `Administrateur` et un mot de passe initial ;
3. cet administrateur crée à son tour les comptes de son organisme.

---

## Les deux niveaux de compte

| | Administrateur plateforme | Compte d'organisme |
|---|---|---|
| `OrganismeId` | `null` | obligatoire |
| `EstAdministrateurPlateforme` | `true` | `false` |
| Droits effectifs | uniquement `DroitHelper.AdmisPlateforme` (comptes, profils, organismes, audit de plateforme) | tous sauf `DroitHelper.ReservesPlateforme` (`OrganismeGerer`, `ProfilGestion`, `NotificationParametrage`) |
| Données budgétaires | **aucune** (le filtre d'organisme ne renvoie rien) | celles de son organisme |

Le filtrage est fait par `DroitServiceEf`, quels que soient les profils attribués : un
profil mal configuré ne peut pas faire franchir la frontière.

> **Les profils sont globaux** (§ 3.7) : les modifier touche tous les organismes. C'est
> pourquoi `ProfilGestion` est réservé à la plateforme. Un administrateur d'organisme
> **attribue** les profils (`ProfilAttribution`), il ne les modifie pas.

### Profils livrés

| Code | Profil | Points saillants |
|---|---|---|
| `ADMIN` | Administrateur | Tous les droits d'organisme ; valide les habilitations au titre de l'administration du SI |
| `ADMIN_PLATEFORME` | Administrateur plateforme | Organismes, comptes, profils ; aucune donnée budgétaire |
| `DIRECTION` | Direction | Lecture de tout, définition des programmes et indicateurs, exports |
| `RESP_FINANCIER` | Responsable du service financier et comptable | Saisie centralisée, demande et validation financière des habilitations |
| `RESP_BUDGET` | Responsable Budget | Prévisions, ajustements, AE/CP, validation des engagements et dépassements |
| `RESP_PROGRAMME` | Responsable de programme | Lecture de son programme, sans saisie |
| `COMPTABLE` | Comptable | Saisie des engagements, paiements, encaissements |
| `AUDITEUR` | Auditeur | Journaux d'audit, lecture, exports |
| `CONSULTATION` | Consultation | Lecture des comptes et profils (socle) |

Ils sont créés s'ils manquent ; ensuite ils appartiennent aux administrateurs (le seed ne
les modifie plus), sauf `ADMIN` et `ADMIN_PLATEFORME`, complétés à chaque démarrage de
tout nouveau droit.

---

## Connexion, blocage, journal

`AuthentificationService.ConnecterAsync` :

1. cherche le compte par identifiant de connexion (`Matricule`) ou par e-mail ;
2. compte bloqué (`BloqueJusquAu` futur) → refus, **sans** vérifier le mot de passe ;
3. vérifie le mot de passe (BCrypt, hors du fil du rendu) ; un identifiant inconnu coûte
   le même temps de calcul, et reçoit **le même message** qu'un mot de passe faux ;
4. échec → `NbEchecsConnexion + 1` ; au `NbTentativesMax`-ième (paramètre de l'organisme,
   5 par défaut), blocage de `Securite:DureeBlocageMinutes` (15 min) ;
5. mot de passe correct mais compte désactivé ou organisme suspendu → refus explicite ;
6. succès → compteur remis à zéro, `DerniereConnexion`, empreinte recalculée si le coût
   BCrypt configuré a augmenté.

Chaque tentative est écrite dans `auth.JournalConnexion` (succès, échec, blocage,
déconnexion), avec l'adresse IP. Consultation : *Administration › Journal des connexions*
(droit `AuditConsultation`). Un administrateur débloque un compte depuis *Utilisateurs*.

En plus du blocage par compte, `ConfigurationSecurite` limite à
`Securite:LimiteTentativesParMinute` (10) les POST de connexion et de réinitialisation par
adresse IP.

---

## Sessions

| Mécanisme | Effet |
|---|---|
| Expiration glissante | Durée fixée à la connexion : `ParametreOrganisme.DureeSessionMinutes` (30 min), conservée à chaque renouvellement. |
| `session.js` | Signale l'activité au serveur (renouvelle le cookie) et soumet la déconnexion après la durée d'inactivité. |
| `OnValidatePrincipal` | À chaque requête HTTP, le cookie est refusé si le compte est désactivé, bloqué, si son mot de passe a changé ou si son organisme est suspendu. |
| `EtatAuthentificationRevalide` | Même contrôle toutes les `Securite:IntervalleRevalidationSecondes` (60 s) pour un circuit ouvert ; la page renvoie alors vers la connexion. |

Le contrôle compare une **empreinte** posée dans le cookie à la connexion (dérivée du mot
de passe, de l'organisme et du niveau du compte) : changer de mot de passe ferme toutes
les sessions ouvertes du compte.

---

## Mots de passe

| Action | Qui | Où |
|---|---|---|
| Changer le sien | tout utilisateur | *Mon mot de passe* (icône clé, bas de la barre latérale) — l'actuel est exigé |
| Mot de passe oublié | tout utilisateur ayant un e-mail | lien de la page de connexion |
| Définir un mot de passe provisoire | droit `UtilisateurGestion` | *Utilisateurs* › icône clé |

Le lien de réinitialisation est protégé par ASP.NET Data Protection, valable
`Securite:DureeLienReinitialisationMinutes` (60 min) et **à usage unique** : il cesse
d'être valide dès que le mot de passe change. Sans `Email:Active = true`, aucun lien
n'est envoyé (un avertissement est journalisé) : l'administrateur définit alors un mot de
passe provisoire.

> Hors développement, renseignez `Email:BaseUrl` : sans elle, aucun lien n'est envoyé (un
> avertissement est journalisé). L'adresse de la requête n'est utilisée qu'en développement,
> car l'en-tête `Host` permet de la falsifier.

La politique (longueur, majuscule, minuscule, chiffre, caractère spécial) se règle dans
`Securite:MotDePasse`.

---

## Organismes

*Plateforme › Organismes* (droit `OrganismeGerer`, Administrateur plateforme uniquement).

La création provisionne, **dans la même transaction**, les données initiales du § 3.7 :
paramètres (session 30 min, 5 tentatives, pagination 20, rétention 12 mois), devise XOF,
seuils 80/90/100 %, axes, natures de dépense, sources RI/RE, unités de mesure
(`ProvisionnementOrganisme.cs`). Les autres listes du § 3.7 viendront avec le module
Paramétrage.

Suspendre ou archiver un organisme ferme immédiatement l'accès de ses comptes ; ses
données restent intactes.

---

## Habilitations (principe des quatre yeux)

Une habilitation donne à un utilisateur un accès en **lecture** ou en **saisie** sur un
périmètre : tout l'organisme, un axe, un programme, une action, un site ou une structure.

```
Demandée ──► validée (service financier) ───┐
    │                                       ├──► Active ──► Révoquée
    └──► validée (administrateur SI) ───────┘
(en attente) ──► Rejetée
```

| Règle | Où elle est garantie |
|---|---|
| Active seulement après **les deux** validations | `HabilitationService.ValiderAsync` |
| Deux personnes **distinctes** | service + contrainte `CK_Habilitation_ValideursDistincts` |
| Le bénéficiaire ne valide pas sa propre habilitation | service |
| Motif obligatoire pour rejeter ou révoquer | service |
| Attributions et retraits journalisés | audit `ATTRIBUTION_DROIT`, `RETRAIT_DROIT`, `REJET`… |

Les validateurs reçoivent une notification `HabilitationAValider`. Les modules métier
interrogeront `IHabilitationService.ObtenirPerimetresActifsAsync(utilisateurId, niveau)`
pour restreindre la saisie aux périmètres actifs (§ 2.6).

> Choix retenu : la double validation s'applique à **tous** les niveaux d'accès, lecture
> comprise. Pour ne l'exiger qu'en saisie, c'est `ValiderAsync` qu'on adapte.

---

## Écrire un service cloisonné

Rien à faire de particulier : injectez `IDbContextFactory<AppDbContext>`, utilisez
`CreateDbContextAsync`, et le contexte est restreint à l'organisme de l'utilisateur.

```csharp
public async Task<List<ElementListe>> ListerAxesAsync()
{
    await using var ctx = await _factory.CreateDbContextAsync();   // organisme déjà renseigné
    return await ctx.Axes.AsNoTracking()
        .OrderBy(a => a.Ordre)
        .Select(a => new ElementListe(a.Id, a.Libelle))
        .ToListAsync();
}
```

- `CreateDbContext()` (synchrone) lève une exception : résoudre l'identité est asynchrone,
  et bloquer gèlerait le circuit.
- La fabrique sans organisme (`[FromKeyedServices(FabriqueContexteOrganisme.CleSansOrganisme)]`)
  est réservée à l'authentification, au seed et aux traitements planifiés.
- Contrôlez les droits **dans le service**, à partir de `IUserContextService` — jamais
  d'un identifiant d'utilisateur passé par l'écran (voir `UtilisateurServiceEf.ActeurAsync`).

---

## Tests

`eProcessBudget.Tests` couvre les tests clés du § 4.1 sur une base EF Core en mémoire :

```bash
dotnet test eProcessBudget.Tests
```

| Fichier | Vérifie |
|---|---|
| `AuthentificationTests` | blocage après N échecs, remise à zéro, message identique, organisme suspendu, session refusée après changement de mot de passe, politique |
| `HabilitationTests` | inactive sans les deux validations, refus de la double validation par une même personne, bénéficiaire, droits, motif, autre organisme |
| `CloisonnementTests` | lecture et écriture isolées entre organismes, fabrique de contexte, frontière plateforme / organisme |

Le cahier prévoit aussi des tests d'intégration sur une base SQL Server locale dédiée
(index filtrés, contraintes CHECK, concurrence) : à ajouter quand cette base existera.
