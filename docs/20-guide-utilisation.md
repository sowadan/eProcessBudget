# 20 — Guide d'utilisation et visites guidées

Le manuel utilisateur est **dans l'application** et suit les droits de chacun : un guide
par écran, lisible sur la page *Guide d'utilisation* (`/aide`) et joué en **visite
guidée** sur l'écran lui-même, avec [driver.js](https://driverjs.com) 1.3.6 (MIT).

---

## Ce que voit l'utilisateur

| Où | Quoi |
|---|---|
| Bouton **Aide** de l'en-tête | *Visite guidée de cet écran*, *Découvrir l'interface*, *Guide d'utilisation* |
| Première connexion | La visite de l'interface se lance une fois (mémorisée par utilisateur dans le navigateur) |
| Menu › *Guide d'utilisation* (`/aide`) | Les guides classés comme le menu, avec recherche ; *Lancer la visite* ouvre l'écran et le présente |

Les visites sont **dynamiques** : une étape dont l'élément n'est pas affiché (bouton
réservé à un droit, tableau vide, section du menu repliée) est sautée, et le manuel ne
liste que les écrans dont l'utilisateur voit l'entrée de menu.

---

## Comment c'est construit

```
Web/Features/Aide/
├── GuidesApplication.cs   ★ le contenu : un GuideUtilisation par écran, la visite de l'interface
├── VisitesGuidees.cs      lancement depuis C# (écran, interface, première visite, ?visite=...)
└── Index.razor            /aide : le manuel, filtré par droits et par recherche

Web/Components/Layout/BoutonAide.razor   bouton de l'en-tête
Web/wwwroot/js/guide.js                  window.epbGuide : résout les éléments, joue la visite
Web/wwwroot/lib/driverjs/                driver.js servi localement (pas de CDN en production)
```

Une visite d'écran enchaîne : la présentation (sur l'en-tête de page), les **éléments
communs** à tous les écrans (actions, chiffres clés, filtres, onglets, liste,
pagination — sautés s'ils sont absents), puis les règles propres à l'écran
(`BonASavoir`). Un écran sans guide reçoit la visite générique.

### Points d'ancrage

| Attribut | Posé par |
|---|---|
| `data-guide="page-entete"`, `"page-actions"` | `PageHeader` (toutes les pages) |
| `data-guide="indicateur"` | `AppStatCard` |
| `data-guide="menu"`, `"menu-replier"`, `"utilisateur"`, `"notifications"`, `"aide"` | `MainLayout`, `BoutonAide` |

Désignation d'un élément dans une étape : sélecteur CSS (premier élément visible),
`carte:<sélecteur>` (la carte `.carte` qui le contient), `groupe:<sélecteur>` (son parent),
ou `null` pour une bulle centrée.

---

## Documenter un écran

1. Ajoutez un `GuideUtilisation` dans `GuidesApplication.Tous` : route (un segment
   `{Id}` accepte toute valeur), titre, résumé, règles à connaître.
2. Pour montrer un élément précis, posez-lui `data-guide="mon-element"` et ajoutez une
   étape `new("[data-guide=\"mon-element\"]", "Titre", "Texte")`.
3. C'est tout : le guide apparaît dans `/aide` pour les utilisateurs qui voient l'écran,
   et le bouton Aide le joue.

Un guide suit les droits de l'entrée de menu de sa route, ou du plus long préfixe (une
fiche `/execution/engagements/{Id}` suit *Engagements*). Les textes sont rédigés côté
serveur et insérés tels quels : balises simples seulement (`strong`, `em`, `br`), jamais
de donnée saisie par un utilisateur.

## Mettre à jour driver.js

Remplacez `driver.js.iife.js` et `driver.css` dans `wwwroot/lib/driverjs/` (depuis
`https://cdn.jsdelivr.net/npm/driver.js@<version>/dist/`) et le numéro de version dans
`App.razor`. Le thème est dans `app.css` (classe `epb-guide`).
