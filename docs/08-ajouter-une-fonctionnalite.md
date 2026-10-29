# 08 — Ajouter une fonctionnalité

Le parcours complet, de l'entité à l'entrée de menu, sur un exemple concret : un module
**Commandes**.

À la fin, vous aurez une page de liste protégée par des droits, avec création,
validation, audit et notification.

---

## Les sept étapes

```
1. Entité            Core/Entities/Commande.cs
2. DbSet             Infrastructure/Data/AppDbContext.cs
3. Migration         dotnet ef migrations add AjoutCommandes
4. Droits            Core/Enums/TypeDroit.cs + DroitHelper.cs
5. Service           Infrastructure/Services/Commandes/CommandeService.cs
6. Enregistrement    Program.cs (ou votre méthode d'extension)
7. Page + menu       Web/Features/Commandes/ + MenuApplication.cs
```

---

## 1. L'entité

`eProcessBudget.Core/Entities/Commande.cs` :

```csharp
using System.ComponentModel.DataAnnotations;

namespace eProcessBudget.Core.Entities;

/// <summary>Commande passée auprès d'un fournisseur.</summary>
public class Commande : EntiteOrganisme
{
    [Required(ErrorMessage = "La référence est obligatoire.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "Entre 3 et 30 caractères.")]
    public string Reference { get; set; } = string.Empty;

    [Required(ErrorMessage = "L'objet est obligatoire.")]
    [StringLength(200)]
    public string Objet { get; set; } = string.Empty;

    [Range(0, 999_999_999, ErrorMessage = "Le montant doit être positif.")]
    public decimal Montant { get; set; }

    public StatutCommande Statut { get; set; } = StatutCommande.Brouillon;

    /// <summary>Utilisateur ayant créé la commande.</summary>
    public Ulid DemandeurId { get; set; }
    public virtual Utilisateur Demandeur { get; set; } = null!;

    public DateTime? DateValidation { get; set; }
    public Ulid? ValideurId { get; set; }
}
```

Et l'énumération de statut dans `Core/Enums/` :

```csharp
public enum StatutCommande : byte   // stockée en tinyint, contrôlée par une contrainte CHECK
{
    Brouillon = 1,
    EnAttente = 2,
    Validee   = 3,
    Rejetee   = 4
}
```

**Points d'attention :**

- une entité métier hérite d'`EntiteOrganisme` : `Id`, champs d'audit, suppression
  logique, `RowVersion` et `OrganismeId` (cloisonnement par organisme) ;
- les annotations de validation vivent **sur le modèle** : elles protègent le formulaire
  *et* toute future API ;
- valeurs d'énumération **persistées** : ne les renumérotez jamais.

---

## 2. Le DbSet et sa configuration

Dans `eProcessBudget.Infrastructure/Data/AppDbContext.cs`, ajoutez le DbSet :

```csharp
public DbSet<Commande> Commandes => Set<Commande>();
```

Puis une classe de configuration, ajoutée au fichier de son module
(`Data/Configurations/Configurations<Module>.cs`).
Elle est découverte automatiquement (`ApplyConfigurationsFromAssembly`) :

```csharp
public class CommandeConfiguration : IEntityTypeConfiguration<Commande>
{
    public void Configure(EntityTypeBuilder<Commande> e)
    {
        e.ToTable("Commande", "bud");   // singulier, schéma du domaine

        // La référence identifie la commande : unicité garantie par la base, pas
        // seulement par le service. OrganismeId en tête : deux organismes peuvent
        // avoir la même référence.
        e.HasIndex(x => new { x.OrganismeId, x.Reference }).IsUnique();

        // Index qui sert l'écran principal : « les commandes à valider, les plus récentes ».
        e.HasIndex(x => new { x.Statut, x.DateCreation });

        e.Property(x => x.Reference).HasMaxLength(30);
        e.Property(x => x.Objet).HasMaxLength(200);

        // Le demandeur est un utilisateur : clé étrangère sans cascade.
        e.HasOne(x => x.Demandeur).WithMany().HasForeignKey(x => x.DemandeurId);
    }
}
```

> **Inutile de répéter ce que fait `AppDbContext` pour toutes les tables** : clé ULID
> `binary(16)`, `OrganismeId` et son filtre, suppression logique, `RowVersion`,
> `NO ACTION`, contrainte CHECK des énumérations, nom `UX_` et filtre des index
> uniques, `decimal(18,2)` par défaut. Voir [09 — Base de données](09-base-de-donnees.md).

---

## 3. La migration

```bash
cd eProcessBudget.Infrastructure
dotnet ef migrations add AjoutCommandes --output-dir Data/Migrations
```

**Relisez le fichier généré** avant de l'appliquer — voir
[09 — Base de données](09-base-de-donnees.md).

```bash
dotnet ef database update
```

---

## 4. Les droits

`Core/Enums/TypeDroit.cs` :

```csharp
// ── Commandes (100-109) ──────────────────────────────────────────
CommandeConsultation = 100,
CommandeCreation     = 101,
CommandeModification = 102,
CommandeSuppression  = 103,
CommandeValidation   = 104,
```

`Core/Enums/DroitHelper.cs`, dans `Libelle` :

```csharp
TypeDroit.CommandeConsultation => "Commandes : consulter",
TypeDroit.CommandeCreation     => "Commandes : créer",
TypeDroit.CommandeModification => "Commandes : modifier",
TypeDroit.CommandeSuppression  => "Commandes : supprimer",
TypeDroit.CommandeValidation   => "Commandes : valider",
```

Et dans `Groupe` :

```csharp
public static string Groupe(TypeDroit droit) => (int)droit switch
{
    < 100 => "Administration",
    < 200 => "Commandes",        // ← ajouté
    _     => "Métier"
};
```

Les nouveaux droits apparaissent automatiquement dans `/administration/profils` :
cet écran énumère l'énumération, il ne code aucune liste.

---

## 5. Le service

`eProcessBudget.Infrastructure/Services/Commandes/CommandeService.cs` :

```csharp
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Securite;
using eProcessBudget.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Commandes;

public interface ICommandeService
{
    Task<(List<Commande> Items, int Total)> ListerAsync(
        string? recherche = null, StatutCommande? statut = null, int page = 1, int taille = 20);

    Task<Commande?> ObtenirAsync(Ulid id);
    Task<(bool Ok, string? Erreur, Commande? Commande)> CreerAsync(Commande commande, Ulid userId);
    Task<(bool Ok, string? Erreur)> ValiderAsync(Ulid id, Ulid userId);
}

public class CommandeService : ICommandeService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IDroitService _droits;
    private readonly IAuditService _audit;
    private readonly INotificationService _notifications;

    public CommandeService(
        IDbContextFactory<AppDbContext> factory,
        IDroitService droits,
        IAuditService audit,
        INotificationService notifications)
    {
        _factory = factory;
        _droits = droits;
        _audit = audit;
        _notifications = notifications;
    }

    // ── Lecture : filtrage et pagination CÔTÉ SERVICE ────────────────────
    public async Task<(List<Commande> Items, int Total)> ListerAsync(
        string? recherche = null, StatutCommande? statut = null, int page = 1, int taille = 20)
    {
        await using var ctx = await _factory.CreateDbContextAsync();

        var q = ctx.Commandes.AsNoTracking()
                   .Include(c => c.Demandeur)
                   .AsQueryable();

        if (statut.HasValue) q = q.Where(c => c.Statut == statut.Value);

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim();
            q = q.Where(c => c.Reference.Contains(r) || c.Objet.Contains(r));
        }

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.DateCreation)
                           .Skip(Math.Max(0, (page - 1) * taille))
                           .Take(taille)
                           .ToListAsync();

        return (items, total);
    }

    public async Task<Commande?> ObtenirAsync(Ulid id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Commandes.AsNoTracking()
            .Include(c => c.Demandeur)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    // ── Écriture ─────────────────────────────────────────────────────────
    public async Task<(bool, string?, Commande?)> CreerAsync(Commande commande, Ulid userId)
    {
        // 1. LE DROIT, AVANT TOUT
        if (!await _droits.ADroitAsync(userId, TypeDroit.CommandeCreation))
            return (false, "Vous n'avez pas le droit de créer une commande.", null);

        await using var ctx = await _factory.CreateDbContextAsync();

        // 2. Les règles métier → message, pas exception
        if (await ctx.Commandes.AnyAsync(c => c.Reference == commande.Reference))
            return (false, $"La référence « {commande.Reference} » est déjà utilisée.", null);

        // 3. Le traitement
        commande.DemandeurId = userId;
        commande.CreePar = userId;
        commande.Statut = StatutCommande.Brouillon;

        ctx.Commandes.Add(commande);
        await ctx.SaveChangesAsync();

        // 4. L'audit
        await _audit.LoggerAsync(userId, null, "CREATION", "Commandes",
            nameof(Commande), commande.Id.ToString(),
            nouvelleValeur: new { commande.Reference, commande.Objet, commande.Montant });

        return (true, null, commande);
    }

    public async Task<(bool, string?)> ValiderAsync(Ulid id, Ulid userId)
    {
        if (!await _droits.ADroitAsync(userId, TypeDroit.CommandeValidation))
        {
            // Journaliser aussi les REFUS : une série de tentatives est un signal.
            await _audit.LoggerAsync(userId, null, "VALIDATION_REFUSEE", "Commandes",
                nameof(Commande), id.ToString(),
                estSucces: false, messageErreur: "Droit manquant");

            return (false, "Vous n'avez pas le droit de valider une commande.");
        }

        await using var ctx = await _factory.CreateDbContextAsync();

        var commande = await ctx.Commandes.FirstOrDefaultAsync(c => c.Id == id);
        if (commande is null) return (false, "Commande introuvable.");

        if (commande.Statut != StatutCommande.EnAttente)
            return (false, "Seule une commande en attente peut être validée.");

        var ancienStatut = commande.Statut;

        commande.Statut = StatutCommande.Validee;
        commande.DateValidation = DateTime.UtcNow;
        commande.ValideurId = userId;
        commande.ModifiePar = userId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(userId, null, "VALIDATION", "Commandes",
            nameof(Commande), id.ToString(),
            ancienneValeur: new { Statut = ancienStatut.ToString() },
            nouvelleValeur: new { Statut = commande.Statut.ToString() });

        // 5. Prévenir le demandeur
        await _notifications.NotifierAsync(
            type:           TypeNotification.Succes,
            message:        $"Votre commande {commande.Reference} a été validée.",
            destinataireId: commande.DemandeurId,
            titre:          "Commande validée",
            lien:           $"/commandes/{commande.Id}",
            cleEntite:      $"Commande:{commande.Id}",
            acteurId:       userId);

        return (true, null);
    }
}
```

**L'ordre compte : droit → règles métier → traitement → audit → notification.**

---

## 6. L'enregistrement

Plutôt que de modifier `ServiceCollectionExtensions` du socle, créez **votre** méthode
d'extension : le câblage du socle reste lisible.

`eProcessBudget.Infrastructure/ServiceCollectionExtensionsMetier.cs` :

```csharp
namespace eProcessBudget.Infrastructure;

public static class ServiceCollectionExtensionsMetier
{
    public static IServiceCollection AddServicesMetier(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICommandeService, CommandeService>();
        return services;
    }
}
```

Dans `Program.cs`, à l'emplacement prévu :

```csharp
builder.Services.AddSocleApplicatif(builder.Configuration, builder.Environment);
builder.Services.AddServicesMetier(builder.Configuration);   // ← ici
```

---

## 7. La page et le menu

### La page

`eProcessBudget.Web/Features/Commandes/Index.razor` :

```razor
@page "/commandes"
@using eProcessBudget.Infrastructure.Services.Commandes
@inject ICommandeService Commandes
@inject IUserContextService UserContext
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<PageTitle>Commandes</PageTitle>

<AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeConsultation }">

    <PageHeader Titre="Commandes" Description="Suivi des commandes fournisseurs.">
        <BreadcrumbContent>
            <a href="/" class="hover:underline">Accueil</a>
            <span>/</span>
            <span class="text-gray-700">Commandes</span>
        </BreadcrumbContent>
        <ActionsContent>
            <AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeCreation }" Discret="true">
                <AppButton Icone="plus" OnClick="CreerAsync">Nouvelle commande</AppButton>
            </AutoriseSi>
        </ActionsContent>
    </PageHeader>

    <AppCard ClasseSup="mb-6">
        <div class="flex flex-col sm:flex-row gap-3">
            <MudTextField T="string" @bind-Value="_recherche" @bind-Value:after="RechercherAsync"
                          Placeholder="Référence ou objet…" Clearable="true"
                          Variant="Variant.Outlined" Margin="Margin.Dense" Class="flex-1" />

            <MudSelect T="StatutCommande?" @bind-Value="_statut" @bind-Value:after="RechercherAsync"
                       Label="Statut" Variant="Variant.Outlined" Margin="Margin.Dense"
                       Clearable="true" Style="min-width:180px;">
                @foreach (var s in Enum.GetValues<StatutCommande>())
                {
                    <MudSelectItem T="StatutCommande?" Value="@s">@s</MudSelectItem>
                }
            </MudSelect>
        </div>
    </AppCard>

    <AppCard PaddingCorps="false">
        <ChildContent>
            @if (_chargement)
            {
                <div class="flex justify-center py-16">
                    <MudProgressCircular Indeterminate="true" Color="Color.Primary" />
                </div>
            }
            else if (_items.Count == 0)
            {
                <EtatVide Icone="clipboard-list" Titre="Aucune commande"
                          Message="Les commandes que vous créez apparaîtront ici." />
            }
            else
            {
                <div class="zone-defilante">
                    <MudTable Items="_items" Dense="true" Hover="true" Elevation="0">
                        <HeaderContent>
                            <MudTh>Référence</MudTh>
                            <MudTh>Objet</MudTh>
                            <MudTh Style="text-align:right">Montant</MudTh>
                            <MudTh>Statut</MudTh>
                            <MudTh Style="text-align:right">Actions</MudTh>
                        </HeaderContent>
                        <RowTemplate>
                            <MudTd DataLabel="Référence">
                                <span class="font-mono text-xs font-semibold">@context.Reference</span>
                            </MudTd>
                            <MudTd DataLabel="Objet">@context.Objet</MudTd>
                            <MudTd DataLabel="Montant" Style="text-align:right">
                                <span class="tabular-nums">@context.Montant.ToString("N0")</span>
                            </MudTd>
                            <MudTd DataLabel="Statut">
                                <AppBadge Variante="@Variante(context.Statut)">@context.Statut</AppBadge>
                            </MudTd>
                            <MudTd DataLabel="Actions" Style="text-align:right">
                                <AutoriseSi DroitsRequis="new[] { TypeDroit.CommandeValidation }" Discret="true">
                                    @if (context.Statut == StatutCommande.EnAttente)
                                    {
                                        <MudIconButton Size="Size.Small" Color="Color.Success"
                                                       Icon="@Icons.Material.Outlined.CheckCircle"
                                                       OnClick="() => ValiderAsync(context)" />
                                    }
                                </AutoriseSi>
                            </MudTd>
                        </RowTemplate>
                    </MudTable>
                </div>
            }
        </ChildContent>
        <PiedContent>
            <div class="flex items-center justify-between gap-4 flex-wrap">
                <p class="text-xs text-gray-500">@_total commande(s)</p>
                @if (_total > _taille)
                {
                    <MudPagination Count="@((int)Math.Ceiling(_total / (double)_taille))"
                                   Selected="_page" SelectedChanged="ChangerPageAsync"
                                   Color="Color.Primary" Size="Size.Small" />
                }
            </div>
        </PiedContent>
    </AppCard>
</AutoriseSi>

@code {
    private const int _taille = 20;

    private List<Commande> _items = new();
    private int _total, _page = 1;
    private bool _chargement = true;

    private string _recherche = "";
    private StatutCommande? _statut;

    protected override async Task OnInitializedAsync() => await ChargerAsync();

    private async Task ChargerAsync()
    {
        _chargement = true;
        (_items, _total) = await Commandes.ListerAsync(_recherche, _statut, _page, _taille);
        _chargement = false;
    }

    // Un changement de filtre ramène à la page 1.
    private async Task RechercherAsync() { _page = 1; await ChargerAsync(); }
    private async Task ChangerPageAsync(int p) { _page = p; await ChargerAsync(); }

    private async Task ValiderAsync(Commande commande)
    {
        var parametres = new DialogParameters<DialogConfirmation>
        {
            { x => x.Titre,   $"Valider la commande {commande.Reference}" },
            { x => x.Message, "Le demandeur en sera informé." },
            { x => x.Libelle, "Valider" }
        };

        var dialog  = await DialogService.ShowAsync<DialogConfirmation>("Confirmation", parametres);
        var reponse = await dialog.Result;
        if (reponse is null || reponse.Canceled) return;

        var userId = await UserContext.ObtenirUserIdAsync();
        var (ok, erreur) = await Commandes.ValiderAsync(commande.Id, userId);

        Snackbar.Add(ok ? "Commande validée." : erreur!, ok ? Severity.Success : Severity.Warning);
        if (ok) await ChargerAsync();
    }

    private async Task CreerAsync()
    {
        // Ouvrir un DialogFormCommande, puis recharger — voir Features/Administration/Profils.razor
    }

    private static string Variante(StatutCommande s) => s switch
    {
        StatutCommande.Validee   => "success",
        StatutCommande.EnAttente => "warning",
        StatutCommande.Rejetee   => "danger",
        _                        => "neutral"
    };
}
```

### Le menu

`Web/Components/Layout/MenuApplication.cs` :

```csharp
new("Commandes", new EntreeMenu[]
{
    new("/commandes", "clipboard-list", "Commandes",
        new[] { TypeDroit.CommandeConsultation }),
}),
```

Placez la section avant `"Administration"` : le métier passe avant l'outillage.

---

## Vérifier

1. `dotnet build`
2. Connectez-vous avec un compte d'organisme portant le profil `ADMIN` → l'entrée
   « Commandes » est là. *(Le seed accorde les nouveaux droits à `ADMIN` au démarrage si
   `MigrerAuDemarrage = true` ; sinon, cochez-les dans* Administration › Profils*.)*
3. Connectez-vous avec un compte sans `CommandeConsultation` → l'entrée a disparu.
4. Ouvrez `/commandes` directement → écran « Accès refusé ».
5. Créez, validez → vérifiez la cloche et `/administration/audit`.

Si les cinq points passent, la fonctionnalité est correctement intégrée au socle.

---

## Liste de contrôle

- [ ] L'entité hérite de `BaseEntity`
- [ ] Annotations de validation **sur le modèle**
- [ ] Configuration EF dans une **méthode dédiée** de `AppDbContext`
- [ ] Index sur les colonnes de filtre et de tri
- [ ] `HasPrecision(18, 2)` sur les `decimal`
- [ ] Migration générée **et relue**
- [ ] Droits déclarés, libellés renseignés, groupe défini
- [ ] Service : vérification du droit **avant** toute écriture
- [ ] Erreurs métier en `(bool Ok, string? Erreur)`, pas en exception
- [ ] Audit sur les écritures **et sur les refus**
- [ ] Notification aux personnes concernées, avec `acteurId`
- [ ] Filtrage et pagination **côté service**
- [ ] Page protégée par `<AutoriseSi>`, actions réservées masquées
- [ ] `EtatVide` distinguant « liste vide » et « aucun résultat »
- [ ] Entrée de menu avec ses droits
- [ ] Service enregistré dans `AddServicesMetier`

