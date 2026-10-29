# 05 — Droits et accès

Que peut faire l'utilisateur courant, et comment le contrôler.

---

## La règle à ne jamais enfreindre

> **Masquer un bouton n'est pas une protection.**
> Chaque action qui lit ou écrit des données sensibles doit revérifier le droit
> **côté service**, indépendamment de ce qui est affiché.

Un service peut être appelé depuis plusieurs écrans, ou demain depuis une API.
`<AutoriseSi>` améliore l'expérience — il ne sécurise rien.

Les deux vont ensemble :

```razor
@* 1. L'affichage : l'utilisateur ne voit que ce qui le concerne *@
<AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeValidation }" Discret="true">
    <AppButton OnClick="Valider">Valider</AppButton>
</AutoriseSi>
```

```csharp
// 2. L'action : la vérification qui compte réellement
public async Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid commandeId, Ulid userId)
{
    if (!await _droits.ADroitAsync(userId, TypeDroit.CommandeValidation))
        return (false, "Vous n'avez pas le droit de valider une commande.");

    // … le traitement
}
```

---

## Le modèle de droits

```
Utilisateur ──< UtilisateurProfil >── Profil ──< ProfilDroit >── TypeDroit
     └────────< UtilisateurDroit >──────────────────────────────  TypeDroit
```

Deux chemins pour obtenir un droit :

| Chemin | Usage | Fréquence |
|---|---|---|
| **Via un profil** | Cas normal. Un paquet de droits réutilisable : « Gestionnaire », « Valideur ». | 95 % |
| **En direct** | Exception datée : intérim, mission ponctuelle, retrait ciblé. | 5 % |

Un `UtilisateurDroit` avec `EstAccorde = false` **retire** un droit hérité d'un profil.
C'est la seule façon de dire « ce gestionnaire-là, mais sans la validation ».

### Calcul des droits effectifs

`DroitServiceEf.ObtenirDroitsAsync` applique, dans cet ordre :

1. les droits des **profils actifs et non expirés** ;
2. plus les droits **directs accordés**, en vigueur ;
3. moins les droits **directs retirés**, en vigueur.

Le retrait explicite gagne toujours.

Le résultat est mis en cache **2 minutes par utilisateur** : la question « a-t-il ce
droit ? » est posée des dizaines de fois par rendu de page. Après avoir modifié les
profils de quelqu'un, appelez `InvaliderCache(userId)` — `ProfilServiceEf` le fait déjà
pour tous les porteurs d'un profil modifié.

---

## Ajouter un droit

**1. Déclarer la valeur** — `Core/Enums/TypeDroit.cs` :

```csharp
public enum TypeDroit
{
    // … socle (0-99)

    // ── Commandes (100-109) ──────────────────────────────────────────
    CommandeConsultation = 100,
    CommandeCreation     = 101,
    CommandeModification = 102,
    CommandeSuppression  = 103,
    CommandeValidation   = 104,
}
```

> **Les valeurs numériques sont persistées en base.** Ne les renumérotez jamais : vous
> transféreriez silencieusement des droits d'une action à une autre. Pour retirer un
> droit, marquez-le `[Obsolete]` et laissez son numéro inutilisé.

Réservez une plage de 10 par domaine : vous en ajouterez.

**2. Ajouter le libellé** — `Core/Enums/DroitHelper.cs` :

```csharp
TypeDroit.CommandeConsultation => "Commandes : consulter",
TypeDroit.CommandeCreation     => "Commandes : créer",
TypeDroit.CommandeValidation   => "Commandes : valider",
```

**3. Classer le droit** dans un groupe d'affichage, toujours dans `DroitHelper` :

```csharp
public static string Groupe(TypeDroit droit) => (int)droit switch
{
    < 100 => "Administration",
    < 200 => "Commandes",       // ← votre plage
    _     => "Métier"
};
```

C'est fini. Le droit apparaît automatiquement dans le catalogue et dans la configuration
des profils : ces écrans énumèrent l'énumération, ils ne codent aucune liste en dur.

**Bonus.** Le profil `ADMIN` reçoit d'office tout nouveau droit : `SeedService` lui
accorde à chaque démarrage (si `MigrerAuDemarrage = true`) toutes les valeurs de
`TypeDroit`. Voir [09 — Base de données](09-base-de-donnees.md) pour l'en exclure.

---

## Protéger une page

### La page entière

```razor
@page "/commandes"

<AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeConsultation }">
    <PageHeader Titre="Commandes" />
    @* … *@
</AutoriseSi>
```

Sans le droit, l'utilisateur voit un écran « Accès refusé » explicite, qui nomme le droit
manquant et propose un retour à l'accueil. Mieux qu'une page blanche ou une erreur.

### Un élément réservé

```razor
<AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeValidation }" Discret="true">
    <AppButton OnClick="Valider">Valider</AppButton>
</AutoriseSi>
```

`Discret="true"` n'affiche **rien** en cas de refus — c'est ce qu'on veut pour un bouton
dans une barre d'actions ou une cellule de tableau.

### Plusieurs droits

`DroitsRequis` est un **OU** : un seul des droits listés suffit.

```razor
@* visible pour qui peut consulter OU éditer *@
<AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeConsultation, TypeDroit.CommandeModification }">
```

Pour un **ET**, imbriquez, ou testez dans le code avec `ATousLesDroitsAsync`.

### Dans le code d'un composant

```razor
@inject IDroitService Droits
@inject IUserContextService UserContext

@if (_peutValider)
{
    <AppButton OnClick="Valider">Valider</AppButton>
}

@code {
    private bool _peutValider;

    protected override async Task OnInitializedAsync()
    {
        var userId = await UserContext.ObtenirUserIdAsync();
        _peutValider = await Droits.ADroitAsync(userId, TypeDroit.CommandeValidation);
    }
}
```

Préférable à `<AutoriseSi>` quand le même droit conditionne plusieurs éléments : une
seule vérification au lieu d'une par composant.

---

## L'entrée de menu

Dans `Components/Layout/MenuApplication.cs` :

```csharp
new("Commandes", new EntreeMenu[]
{
    new("/commandes", "clipboard-list", "Commandes",
        new[] { TypeDroit.CommandeConsultation }),

    new("/commandes/valider", "check-circle", "À valider",
        new[] { TypeDroit.CommandeValidation }),
}),
```

`MainLayout` résout les droits **une fois** à l'initialisation, avec un cache par droit,
et masque les entrées refusées. Une section dont aucune entrée n'est visible disparaît
entièrement, titre compris.

Une entrée sans droit (`Droits = null`) est visible par tout utilisateur.

> Rappel : le menu est du confort. La page doit se protéger elle-même.

---

## Vérifier dans un service

C'est ici que la sécurité se joue réellement.

```csharp
public class CommandeService
{
    private readonly IDroitService _droits;
    private readonly IAuditService _audit;

    public async Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid commandeId, Ulid userId)
    {
        // 1. Le droit, avant tout
        if (!await _droits.ADroitAsync(userId, TypeDroit.CommandeValidation))
        {
            await _audit.LoggerAsync(userId, null, "VALIDATION_REFUSEE", "Commandes",
                nameof(Commande), commandeId.ToString(),
                estSucces: false, messageErreur: "Droit manquant");

            return (false, "Vous n'avez pas le droit de valider une commande.");
        }

        // 2. Les règles métier
        // 3. Le traitement
        // 4. L'audit du succès
    }
}
```

Journaliser aussi les **refus** : une série de tentatives refusées sur une même action
est un signal à ne pas perdre.

### Le périmètre de saisie

Le droit ne suffit pas pour une **saisie** budgétaire : l'utilisateur doit aussi détenir
une habilitation de saisie **active** dont le périmètre couvre l'élément saisi
(organisme entier, programme, action, axe, structure ou site).

```csharp
var refus = await _perimetre.VerifierSaisieAsync(userId,
    new CiblePerimetre(ProgrammeId: action.ProgrammeId, ActionId: action.Id));
if (refus is not null) return (false, refus);
```

`IControlePerimetre` renvoie `null` si la saisie est couverte, sinon le message à afficher.
`CiblePerimetre.Organisme` désigne une saisie sans rattachement (elle exige alors une
habilitation « organisme entier »). La consultation n'est pas filtrée par le périmètre.
Voir les cas du budget-programme dans [12-budget-programme.md](12-budget-programme.md).

---

## Gérer les profils dans l'interface

`/administration/profils` permet de créer un profil et de cocher ses droits, groupés
par domaine, avec une case « tout le groupe ».

Deux droits gouvernent cet écran :

| Droit | Permet |
|---|---|
| `ProfilConsultation` | Voir les profils et leur contenu. |
| `ProfilGestion` | Créer, modifier, supprimer, configurer les droits. **Administrateur plateforme uniquement** : les profils sont partagés par tous les organismes. |

Contraintes appliquées par `ProfilServiceEf` :

- un profil `EstSysteme` (livré par le socle) ne peut pas être supprimé ;
- un profil attribué à des utilisateurs ne peut pas être supprimé — retirez-le d'abord ;
- le code d'un profil est unique et non modifiable après création ;
- la suppression est **logique** : la ligne reste, masquée par le filtre global.

---

## Qui est l'utilisateur courant

L'utilisateur connecté, lu dans le cookie de session par `IUserContextService` ; ses
droits sont calculés par `IDroitService`. Un anonyme ou un compte désactivé donne
`Ulid.Empty` : aucun droit.

`IDroitService` borne ensuite les droits au **niveau du compte** : un Administrateur
plateforme ne garde que `DroitHelper.AdmisPlateforme` (aucun droit budgétaire), un compte
d'organisme perd `DroitHelper.ReservesPlateforme` (`OrganismeGerer`, `ProfilGestion`,
`NotificationParametrage`).
Détails : [04 — Plateforme et authentification](04-authentification.md).

Dans un service, ne faites pas confiance à un identifiant d'utilisateur reçu de l'écran :
demandez l'acteur à `IUserContextService` (voir `UtilisateurServiceEf.ActeurAsync`).

---

## Erreurs fréquentes

| Erreur | Conséquence | À faire |
|---|---|---|
| Ne protéger que l'affichage | Faille : l'action reste appelable | Vérifier aussi dans le service. |
| Renuméroter l'énumération | Des droits changent de sens en base, silencieusement | Ne jamais renuméroter ; marquer `[Obsolete]`. |
| Un droit par écran | Catalogue ingérable, profils illisibles | Un droit par **action** (Consultation, Création, Validation…). |
| Oublier `InvaliderCache` | Un droit modifié met 2 minutes à prendre effet | Appeler `InvaliderCache(userId)` après modification. |

