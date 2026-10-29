namespace eProcessBudget.Core.Entities;

/// <summary>
/// Classe de base de toutes les entités persistées : identifiant et champs d'audit
/// (création / modification / suppression logique).
///
/// La suppression est <b>logique</b> : on positionne <see cref="EstSupprime"/> plutôt que
/// de supprimer la ligne. Le DbContext applique un filtre global qui masque automatiquement
/// les entités supprimées (voir AppDbContext.OnModelCreating).
///
/// L'identifiant est un ULID : unique sans coordination, triable par date de création.
/// <see cref="Ulid.Empty"/> signifie « pas encore enregistré » ; la valeur est attribuée
/// à l'ajout par EF Core (UlidValueGenerator).
/// </summary>
public abstract class BaseEntity
{
    public Ulid Id { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public DateTime? DateModification { get; set; }
    public Ulid? CreePar { get; set; }
    public Ulid? ModifiePar { get; set; }

    public bool EstSupprime { get; set; }
    public DateTime? DateSuppression { get; set; }
    public Ulid? SupprimePar { get; set; }

    /// <summary>
    /// Jeton de concurrence optimiste (ROWVERSION), renseigné par SQL Server à chaque écriture.
    /// Un conflit lève DbUpdateConcurrencyException : l'écran invite alors à recharger.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Marque une entité rattachée à un organisme (locataire). Le DbContext en déduit :
/// la clé étrangère vers plat.Organisme, le filtre global sur l'organisme courant et le
/// contrôle d'écriture (voir AppDbContext et cahier technique § 2.3).
/// </summary>
public interface IEntiteOrganisme
{
    Ulid OrganismeId { get; set; }
}

/// <summary>
/// Entité métier complète d'un organisme : colonnes standard du socle (audit, suppression
/// logique, RowVersion) + OrganismeId. Base de presque toutes les tables des modules.
/// </summary>
public abstract class EntiteOrganisme : BaseEntity, IEntiteOrganisme
{
    public Ulid OrganismeId { get; set; }
}

/// <summary>
/// Table allégée (cahier technique § 3.2) : liaisons et journaux en ajout seul.
/// Ni suppression logique, ni modification, ni RowVersion : une liaison retirée est
/// supprimée physiquement, ce qui est tracé par le journal d'audit.
/// </summary>
public abstract class EntiteLiaison : IEntiteOrganisme
{
    public Ulid Id { get; set; }
    public Ulid OrganismeId { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public Ulid? CreePar { get; set; }
}

/// <summary>
/// Référentiel doté d'un statut actif / inactif (cahier fonctionnel § 3.2.1). Un élément
/// inactif reste en base et dans l'historique, mais n'est plus proposé à la saisie.
/// </summary>
public interface IActivable
{
    bool EstActif { get; set; }
}
