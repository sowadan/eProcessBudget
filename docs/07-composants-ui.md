# 07 — Composants d'interface

Catalogue des composants réutilisables et conventions visuelles du socle.

> Les écrans d'administration (`Features/Administration/`) utilisent la plupart de ces
> composants : c'est le meilleur endroit pour les voir en situation.

---

## Les deux bibliothèques, et quand utiliser laquelle

| | Composants `App*` (maison) | MudBlazor |
|---|---|---|
| **Pour** | Mise en page, présentation, actions | Saisie, tableaux, dialogues |
| **Style** | Tailwind + charte configurable | Thème MudBlazor construit depuis la charte |
| **Exemples** | `AppButton`, `AppCard`, `AppBadge`, `EtatVide` | `MudTextField`, `MudTable`, `MudDatePicker` |

**Règle simple :** ce qui **affiche** → composants maison. Ce qui **saisit** → MudBlazor.

Les deux lisent la même charte : ils s'accordent visuellement sans réglage.

---

## Mise en page

### `PageHeader`

En-tête standard : fil d'Ariane, titre, description, actions.

```razor
<PageHeader Titre="Commandes" Description="Suivi des commandes en cours">
    <BreadcrumbContent>
        <a href="/" class="hover:underline">Accueil</a>
        <span>/</span>
        <span class="text-gray-700">Commandes</span>
    </BreadcrumbContent>
    <ActionsContent>
        <AppButton Icone="plus" OnClick="Creer">Nouvelle commande</AppButton>
    </ActionsContent>
</PageHeader>
```

> Le `<h1>` de la page est **ici et nulle part ailleurs** : `Routes.razor` y place le
> focus après chaque navigation, ce qui annonce la nouvelle page aux lecteurs d'écran.

### `AppCard`

Le conteneur de base. Quatre zones : titre, actions, corps, pied.

```razor
@* Simple *@
<AppCard>Contenu</AppCard>

@* Avec titre *@
<AppCard Titre="Détail" SousTitre="Informations générales">…</AppCard>

@* Tableau pleine largeur : pas de marge intérieure *@
<AppCard PaddingCorps="false">
    <MudTable …/>
</AppCard>

@* Zones nommées *@
<AppCard Titre="Membres">
    <ActionsContent><AppButton Taille="sm" Icone="plus">Ajouter</AppButton></ActionsContent>
    <ChildContent>…</ChildContent>
    <PiedContent><p class="text-xs text-gray-500">12 membres</p></PiedContent>
</AppCard>
```

> ⚠️ **Piège Razor.** Dès que vous utilisez **une** zone nommée, **tout** le contenu doit
> être dans des zones nommées — d'où le `<ChildContent>` explicite ci-dessus. Sinon :
> *« Unrecognized child content inside component 'AppCard' »*.

| Paramètre | Effet |
|---|---|
| `Titre`, `SousTitre` | En-tête simple. |
| `PaddingCorps` | `false` pour coller le contenu aux bords. |
| `ClasseSup` | Classes Tailwind ajoutées (`mb-6`, `lg:col-span-2`…). |

---

## Actions

### `AppButton`

```razor
<AppButton Icone="plus" OnClick="Creer">Nouvelle commande</AppButton>
<AppButton Variante="danger" Icone="trash" EnChargement="_suppression">Supprimer</AppButton>
<AppButton Variante="ghost" Taille="sm">Annuler</AppButton>
```

| Variante | Usage |
|---|---|
| `primary` *(défaut)* | L'action principale. **Une seule par écran.** |
| `secondary` | Action complémentaire. |
| `danger` | Action destructrice. |
| `outline`, `outline-primary` | Action secondaire discrète. |
| `ghost` | Annuler, retour — sans poids visuel. |

Tailles : `sm`, `md` *(défaut)*, `lg`.

**`EnChargement="true"`** désactive le bouton et affiche un indicateur : c'est la
protection la plus simple contre le double envoi d'un formulaire. Utilisez-la sur toute
action asynchrone.

### `DialogConfirmation`

Avant toute action destructrice.

```razor
@inject IDialogService DialogService

var parametres = new DialogParameters<DialogConfirmation>
{
    { x => x.Titre,   $"Supprimer la commande {commande.Reference}" },
    { x => x.Message, "Cette action est définitive et ne peut pas être annulée." },
    { x => x.Libelle, "Supprimer" },
    { x => x.Danger,  true }
};

var dialog  = await DialogService.ShowAsync<DialogConfirmation>("Confirmation", parametres);
var reponse = await dialog.Result;
if (reponse is { Canceled: false })
{
    // confirmé
}
```

Un titre qui **nomme l'objet** vaut mieux qu'un « Êtes-vous sûr ? ».

---

## Présentation de données

### `AppBadge`

```razor
<AppBadge Variante="success">Validé</AppBadge>
<AppBadge Variante="warning" Petit="true">En attente</AppBadge>
```

| Variante | Sens |
|---|---|
| `neutral` *(défaut)* | Information sans jugement. |
| `success` | Terminé, validé, conforme. |
| `warning` | En attente, à surveiller. |
| `danger` | Rejeté, en erreur, en retard. |
| `info` | Neutre mais notable. |
| `primary` | Couleur de la charte, à réserver aux mises en avant. |

**Tenez-vous à une correspondance couleur/sens stable** dans toute l'application : c'est
ce qui rend un tableau lisible d'un coup d'œil.

### `AppStatCard`

```razor
<AppStatCard Label="Commandes en attente" Valeur="18" Icone="clipboard-list"
             SousTitre="dont 3 en retard"
             Variation="12 %" VariationPositive="false"
             CouleurFond="#FEF3C7" CouleurIcone="#92400E" />
```

Règles :
- **quatre tuiles maximum par ligne** ;
- le `Label` dit **ce que compte le chiffre**, pas une catégorie (« Commandes en
  attente », pas « Commandes ») ;
- `CouleurFond` est une teinte claire de `CouleurIcone`.

Palette cohérente :

| Sens | Fond | Icône |
|---|---|---|
| Neutre / information | `#E0F2FE` | `#075985` |
| Positif | `#DCFCE7` | `#166534` |
| Attention | `#FEF3C7` | `#92400E` |
| Négatif | `#FEE2E2` | `#991B1B` |
| Secondaire | `#EDE9FE` | `#5B21B6` |

### `InfoChamp`

```razor
<InfoChamp Label="Matricule" Valeur="@u.Matricule" />

<InfoChamp Label="Statut">
    <AppBadge Variante="success">Actif</AppBadge>
</InfoChamp>
```

Une valeur vide affiche un tiret cadratin — jamais un blanc, qui ferait croire à un bug.

### `EtatVide`

```razor
<EtatVide Icone="collection"
          Titre="Aucune commande"
          Message="Les commandes que vous créez apparaîtront ici.">
    <AppButton Icone="plus" OnClick="Creer">Créer une commande</AppButton>
</EtatVide>
```

**Ne laissez jamais un tableau vide sans explication** : ça ressemble à une panne.
Distinguez les deux cas :

```razor
<EtatVide Titre="@(_filtre ? "Aucun résultat" : "Aucune commande")"
          Message="@(_filtre
                     ? "Aucune commande ne correspond à ces critères."
                     : "Créez votre première commande.")" />
```

---

## Messages à l'utilisateur

### `AppAlert` — persistant, dans le flux de la page

```razor
<AppAlert Variante="warning" Titre="Mode démonstration" Fermable="true">
    Les données ne sont pas enregistrées.
</AppAlert>
```

Variantes : `info` *(défaut)*, `success`, `warning`, `danger`.

### `ISnackbar` — éphémère, après une action

```razor
@inject ISnackbar Snackbar

Snackbar.Add("Commande enregistrée.", Severity.Success);
Snackbar.Add("Le champ Montant est obligatoire.", Severity.Warning);
```

**Le bon choix :**

| Situation | Composant |
|---|---|
| Retour immédiat après un clic | `ISnackbar` |
| Condition qui dure (mode dégradé, période close, erreurs de formulaire) | `AppAlert` |
| Information qui doit survivre à un rechargement | `AppAlert` |

---

## Contrôle d'accès

### `AutoriseSi`

```razor
@* Protéger une page : écran « Accès refusé » si le droit manque *@
<AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeConsultation }">
    …
</AutoriseSi>

@* Masquer un élément : rien n'est affiché *@
<AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeValidation }" Discret="true">
    <AppButton OnClick="Valider">Valider</AppButton>
</AutoriseSi>
```

`DroitsRequis` est un **OU**. Détails : [05 — Droits et accès](05-droits-acces.md).

> Rappel : ce composant protège l'affichage. L'action doit revérifier côté service.

---

## Icônes

### `AppIcon`

```razor
<div class="w-5 h-5 text-gray-600"><AppIcon Nom="bell" /></div>
```

Le SVG hérite de `currentColor` et remplit son conteneur : **c'est le parent qui fixe la
taille et la couleur.**

Icônes disponibles : `home`, `collection`, `document-text`, `sparkles`, `users`,
`shield-check`, `bell`, `search`, `chart-bar`, `plus`, `pencil`, `trash`,
`check-circle`, `exclamation`, `information-circle`, `x`, `download`, `cog`,
`clipboard-list`.

**En ajouter une :** copiez le `<path>` depuis [heroicons.com](https://heroicons.com)
(style *Outline*, 24×24) dans un nouveau `case` de `Components/Shared/AppIcon.razor`.
Un nom inconnu affiche un point d'interrogation plutôt qu'un trou silencieux.

### `MudIcon`

Pour les icônes **à l'intérieur** des composants MudBlazor :

```razor
<MudIconButton Icon="@Icons.Material.Outlined.Edit" Size="Size.Small" OnClick="Modifier" />
```

---

## Formulaires

Le socle s'appuie sur `EditForm` + `DataAnnotationsValidator` + champs MudBlazor.

**Les règles vivent sur le modèle**, pas dans la vue : la même règle protège alors le
formulaire et une éventuelle API.

```csharp
public class Commande
{
    [Required(ErrorMessage = "La référence est obligatoire.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "Entre 3 et 20 caractères.")]
    public string Reference { get; set; } = string.Empty;

    [Range(0, 999_999_999, ErrorMessage = "Le montant doit être positif.")]
    public decimal Montant { get; set; }
}
```

```razor
<EditForm Model="_commande" OnValidSubmit="EnregistrerAsync" OnInvalidSubmit="SurErreur">
    <DataAnnotationsValidator />

    @if (_afficherResumeErreurs)
    {
        <AppAlert Variante="danger" Titre="Le formulaire comporte des erreurs">
            <ValidationSummary class="text-sm" />
        </AppAlert>
    }

    <MudTextField @bind-Value="_commande.Reference" For="@(() => _commande.Reference)"
                  Label="Référence" Variant="Variant.Outlined" Immediate="true" />

    <AppButton Type="submit" EnChargement="_enregistrement">Enregistrer</AppButton>
</EditForm>
```

Points d'attention :

- `For="@(() => …)"` relie le champ à sa règle — sans lui, pas de message d'erreur ;
- `Immediate="true"` valide à la frappe ; sans lui, à la perte de focus ;
- le **résumé d'erreurs** n'apparaît qu'après une tentative d'envoi, pas pendant la
  saisie — c'est le rôle de `OnInvalidSubmit` ;
- une erreur **métier** renvoyée par le service s'affiche dans une `AppAlert` et le
  formulaire reste ouvert, pour corriger sans tout ressaisir.

Voir `Features/Administration/DialogUtilisateur.razor` pour un formulaire complet dans un dialogue.

---

## Tableaux

```razor
<AppCard PaddingCorps="false">
    <ChildContent>
        @* Le tableau défile chez lui : la page ne défile jamais latéralement *@
        <div class="zone-defilante">
            <MudTable Items="_items" Dense="true" Hover="true" Elevation="0">
                <HeaderContent>
                    <MudTh>Référence</MudTh>
                    <MudTh Style="text-align:right">Montant</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd DataLabel="Référence">@context.Reference</MudTd>
                    <MudTd DataLabel="Montant" Style="text-align:right">
                        <span class="tabular-nums">@context.Montant.ToString("N0")</span>
                    </MudTd>
                </RowTemplate>
            </MudTable>
        </div>
    </ChildContent>
    <PiedContent>
        <MudPagination Count="@_nbPages" Selected="_page" SelectedChanged="ChangerPageAsync" />
    </PiedContent>
</AppCard>
```

Conventions :

- `Dense="true"`, `Elevation="0"` : la carte porte déjà l'ombre ;
- `DataLabel` sur chaque `MudTd` : c'est ce qui rend le tableau lisible sur mobile ;
- montants **alignés à droite**, avec `tabular-nums` pour que les chiffres s'alignent
  verticalement ;
- **pagination côté service**, jamais `Items` avec 10 000 lignes.

---

## Charte graphique

Quatre variables CSS, injectées par `App.razor` depuis `appsettings.json` :

| Variable | Rôle |
|---|---|
| `--app-primaire` | Actions principales, accents, menu actif. |
| `--app-secondaire` | Barre latérale, en-têtes, titres. |
| `--app-tertiaire` | Dégradés, variantes foncées. |
| `--app-accent` | Avatars, mises en avant. |

```razor
<div style="background-color: var(--app-primaire);">…</div>
<div style="border-color: color-mix(in srgb, var(--app-primaire) 20%, transparent);">…</div>
```

> **N'écrivez jamais une couleur en dur.** Un `#C10A27` codé en dur rendra le changement
> de charte incomplet et incohérent. Les gris de structure (`text-gray-500`,
> `border-gray-200`) restent en Tailwind : ils ne font pas partie de la charte.

---

## Tailwind : une feuille figée

L'application ne charge plus `cdn.tailwindcss.com`. `App.razor` sert
`wwwroot/css/tailwind.min.css`, une feuille **générée et versionnée** qui ne contient que les
classes réellement employées (environ 26 Ko) : pas de dépendance à un CDN (souvent bloqué en
réseau d'entreprise), pas de compilation dans le navigateur, et une politique de contenu (CSP)
sans domaine de script externe.

| Fichier | Rôle |
|---|---|
| `eProcessBudget.Web/package.json` | Outillage : Tailwind **3.4.19** (version figée), scripts `css` et `css:watch`. |
| `eProcessBudget.Web/tailwind.config.js` | Fichiers analysés (`Components`, `Features`, `Securite`, `wwwroot/js`) et couleurs de la charte. |
| `eProcessBudget.Web/Styles/tailwind.source.css` | Source (`@tailwind base; components; utilities`), hors `wwwroot` : jamais publiée. |
| `eProcessBudget.Web/wwwroot/css/tailwind.min.css` | Feuille générée, servie avec empreinte par `MapStaticAssets`. |

### Installer l'outillage (une fois par poste)

```bash
cd eProcessBudget.Web
npm install
```

Ensuite, **chaque compilation** du projet Web régénère la feuille si un `.razor`, un `.cs`
ou un script a changé (cible MSBuild `GenererTailwind`, incrémentale). Sans `node_modules`
(poste sans Node, serveur de build), la cible est ignorée et la feuille versionnée est
utilisée telle quelle.

Pendant une session de mise en page, `npm run css:watch` régénère à chaque enregistrement.

### La règle à respecter

Tailwind ne génère que les classes qu'il **lit en toutes lettres** dans les fichiers analysés.
Écrivez toujours le nom complet :

```razor
@* Correct : les deux classes figurent dans le source. *@
<span class="@(actif ? "bg-green-100" : "bg-gray-100")">...</span>

@* Incorrect : « bg-green-100 » n'apparaît nulle part, la classe ne sera pas générée. *@
<span class="bg-@(couleur)-100">...</span>
```

Une classe absente ne provoque aucune erreur : l'élément perd simplement son style. En cas de
doute, cherchez-la dans `tailwind.min.css`.

> Les couleurs de la charte restent des **variables CSS** (`bg-app-primaire` vaut
> `var(--app-primaire)`) : elles viennent d'`appsettings.json` à l'exécution. Changer la
> charte ne demande pas de régénérer la feuille.

---

## Créer votre propre composant

Dans `Components/UI/`, en suivant les conventions du socle :

```razor
@*
    Description en une phrase de ce que fait le composant.

    EXEMPLE
    -------
    <MonComposant Titre="Exemple" Variante="success" />
*@

<div class="@Classes">
    @ChildContent
</div>

@code {
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Titre { get; set; } = "";

    /// <summary>neutral | success | warning | danger</summary>
    [Parameter] public string Variante { get; set; } = "neutral";

    /// <summary>Classes Tailwind ajoutées au conteneur.</summary>
    [Parameter] public string ClasseSup { get; set; } = "";

    private string Classes => Variante switch
    {
        "success" => $"rounded-lg bg-green-50 {ClasseSup}",
        _         => $"rounded-lg bg-gray-50 {ClasseSup}"
    };
}
```

Conventions :

| Point | Règle |
|---|---|
| Nom | Préfixe `App` pour un composant générique. |
| En-tête | Un commentaire `@* *@` avec un exemple d'appel. |
| Paramètres | En français, documentés quand le nom ne suffit pas. |
| Variantes | Une `string` documentée, pas une énumération : plus léger à l'appel. |
| `ClasseSup` | Toujours prévoir cette échappatoire pour les marges. |
| Couleurs | Variables CSS, jamais de valeur en dur. |

Documentez son appel dans ce fichier : c'est la meilleure documentation d'un composant.

