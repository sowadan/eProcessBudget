using System.Text.Json;
using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace eProcessBudget.Infrastructure.Data;

/// <summary>
/// Journalisation automatique des écritures (cahier technique § 2.7) : à chaque enregistrement,
/// toute création, modification, suppression logique ou suppression d'une table métier est
/// écrite dans aud.JournalAudit, dans la même transaction, avec les valeurs avant et après en
/// JSON (champs modifiés seulement pour une modification).
///
/// Sont exclus : les journaux eux-mêmes et les notifications ; les champs sensibles (mot de
/// passe, jetons, contenu des fichiers) ; les colonnes techniques (dates et auteurs de
/// création / modification, RowVersion), qui servent à renseigner l'auteur de la ligne.
/// Ces lignes portent le module « Données » ; les événements métier (validations, exports,
/// clôtures...) restent journalisés explicitement par les services.
/// </summary>
internal static class JournalisationAutomatique
{
    public const string Module = "Données";

    /// <summary>Entités jamais journalisées automatiquement.</summary>
    private static readonly HashSet<Type> TypesExclus = [typeof(AuditLog), typeof(JournalConnexion), typeof(Notification)];

    /// <summary>Champs jamais écrits dans le journal : secrets et contenus binaires.</summary>
    private static readonly HashSet<string> ChampsSensibles = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "SecurityStamp", "Contenu", "Empreinte",
    };

    /// <summary>Colonnes standard : elles désignent l'auteur, sans être des changements métier.</summary>
    private static readonly HashSet<string> ColonnesTechniques =
    [
        nameof(BaseEntity.DateCreation), nameof(BaseEntity.CreePar), nameof(BaseEntity.DateModification), nameof(BaseEntity.ModifiePar),
        nameof(BaseEntity.DateSuppression), nameof(BaseEntity.SupprimePar), nameof(BaseEntity.RowVersion),
    ];

    /// <summary>Champs de suivi de connexion : ils changent à chaque connexion, le journal des connexions les trace déjà.</summary>
    private static readonly HashSet<string> ChampsConnexion = ["DerniereConnexion", "NbEchecsConnexion", "BloqueJusquAu"];

    /// <summary>Refuse toute modification ou suppression d'une ligne de journal (ajout seul, § 2.7).</summary>
    public static void VerifierAjoutSeul(EntityEntry entree)
    {
        if (entree.Entity is AuditLog or JournalConnexion && entree.State is EntityState.Modified or EntityState.Deleted)
            throw new InvalidOperationException("Les journaux d'audit et de connexion sont en ajout seul : ils ne se modifient ni ne se suppriment.");
    }

    /// <summary>Lignes de journal décrivant les écritures en attente du contexte.</summary>
    public static List<AuditLog> Decrire(AppDbContext ctx, IEnumerable<EntityEntry> entrees)
    {
        var lignes = new List<AuditLog>();
        var maintenant = DateTime.UtcNow;
        foreach (var e in entrees)
        {
            if (TypesExclus.Contains(e.Metadata.ClrType) || e.State is EntityState.Unchanged or EntityState.Detached) continue;

            string action;
            Dictionary<string, object?>? avant = null, apres = null;
            switch (e.State)
            {
                case EntityState.Added:
                    action = "CREATION";
                    apres = Valeurs(e, p => p.CurrentValue, tous: true);
                    break;
                case EntityState.Deleted:
                    action = "SUPPRESSION";
                    avant = Valeurs(e, p => p.OriginalValue, tous: true);
                    break;
                default:
                    var supprime = e.Entity is BaseEntity && e.Property(nameof(BaseEntity.EstSupprime)) is { IsModified: true } s
                                   && s.CurrentValue is true && s.OriginalValue is false;
                    if (supprime)
                    {
                        action = "SUPPRESSION_LOGIQUE";
                        avant = Valeurs(e, p => p.OriginalValue, tous: true);
                        avant.Remove(nameof(BaseEntity.EstSupprime));
                    }
                    else
                    {
                        action = "MODIFICATION";
                        avant = Valeurs(e, p => p.OriginalValue, tous: false);
                        apres = Valeurs(e, p => p.CurrentValue, tous: false);
                        if (apres.Count == 0) continue;   // seules des colonnes techniques ont bougé
                    }
                    break;
            }

            var (organisme, auteur) = Origine(ctx, e, action);
            lignes.Add(new AuditLog
            {
                Id = Ulid.NewUlid(),
                OrganismeId = organisme,
                UtilisateurId = auteur,
                Action = action,
                Module = Module,
                Entite = e.Metadata.ClrType.Name,
                EntiteId = e.Metadata.FindPrimaryKey()?.Properties.Count == 1 ? Convert.ToString(e.Property(e.Metadata.FindPrimaryKey()!.Properties[0].Name).CurrentValue) : null,
                AncienneValeur = avant is null ? null : JsonSerializer.Serialize(avant),
                NouvelleValeur = apres is null ? null : JsonSerializer.Serialize(apres),
                AdresseIP = ctx.AdresseIp,
                CorrelationId = ctx.CorrelationId,
                DateAction = maintenant,
                EstSucces = true,
            });
        }
        return lignes;
    }

    /// <summary>
    /// Valeurs journalisées d'une entrée : toutes (création, suppression) ou seulement celles
    /// qui ont changé (modification) ; champs sensibles et colonnes techniques exclus.
    /// </summary>
    private static Dictionary<string, object?> Valeurs(EntityEntry e, Func<PropertyEntry, object?> lire, bool tous)
    {
        var valeurs = new Dictionary<string, object?>();
        foreach (var p in e.Properties)
        {
            var nom = p.Metadata.Name;
            if (ChampsSensibles.Contains(nom) || ColonnesTechniques.Contains(nom) || p.Metadata.IsShadowProperty()) continue;
            if (nom.Contains("Jeton", StringComparison.OrdinalIgnoreCase) || nom.Contains("Token", StringComparison.OrdinalIgnoreCase)) continue;
            if (!tous && (!p.IsModified || Equals(p.OriginalValue, p.CurrentValue) || ChampsConnexion.Contains(nom))) continue;
            var valeur = lire(p);
            if (tous && valeur is null) continue;
            valeurs[nom] = Lisible(valeur);
        }
        return valeurs;
    }

    /// <summary>Valeur sérialisable et lisible : identifiants et dates en texte, énumérations par leur nom.</summary>
    private static object? Lisible(object? valeur) => valeur switch
    {
        null => null,
        Ulid u => u.ToString(),
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss"),
        Enum en => en.ToString(),
        byte[] => "(binaire)",
        _ => valeur,
    };

    /// <summary>Organisme de la ligne et auteur de l'écriture (colonnes standard de l'entité).</summary>
    private static (Ulid? Organisme, Ulid? Auteur) Origine(AppDbContext ctx, EntityEntry e, string action)
    {
        Ulid? organisme = e.Entity switch
        {
            IEntiteOrganisme o => o.OrganismeId,
            Utilisateur u => u.OrganismeId,
            _ => ctx.OrganismeCourantId,
        };
        if (organisme == Ulid.Empty) organisme = ctx.OrganismeCourantId == Ulid.Empty ? null : ctx.OrganismeCourantId;

        Ulid? auteur = e.Entity switch
        {
            BaseEntity b => action switch
            {
                "CREATION" => b.CreePar,
                "SUPPRESSION_LOGIQUE" => b.SupprimePar ?? b.ModifiePar,
                _ => b.ModifiePar ?? b.CreePar,
            },
            EntiteLiaison l => l.CreePar,
            _ => null,
        };
        return (organisme, auteur);
    }
}
