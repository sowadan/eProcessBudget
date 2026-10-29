using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace eProcessBudget.Infrastructure.Data;

/// <summary>
/// Stocke un <see cref="Ulid"/> en BINARY(16) (cahier technique § 2.4).
///
/// Les 48 premiers bits d'un ULID sont un horodatage big-endian : le tri binaire de SQL
/// Server suit l'ordre de création, ce qui limite la fragmentation de l'index cluster.
/// (Un UNIQUEIDENTIFIER perdrait cet ordre ; un CHAR(26) coûterait 10 octets de plus par clé.)
/// Dans l'interface et les URL, l'ULID reste exposé sous sa forme de 26 caractères.
/// </summary>
public class UlidToBytesConverter : ValueConverter<Ulid, byte[]>
{
    public UlidToBytesConverter()
        : base(u => u.ToByteArray(), b => new Ulid(b))
    {
    }
}

/// <summary>
/// Attribue un nouvel ULID à toute entité ajoutée dont l'Id vaut encore <see cref="Ulid.Empty"/>.
/// La valeur est définitive dès l'Add : on peut l'utiliser comme clé étrangère avant le SaveChanges.
/// </summary>
public class UlidValueGenerator : ValueGenerator<Ulid>
{
    public override bool GeneratesTemporaryValues => false;

    public override Ulid Next(EntityEntry entry) => Ulid.NewUlid();
}
