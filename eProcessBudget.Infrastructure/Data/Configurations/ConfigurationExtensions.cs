using System.Linq.Expressions;
using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

/// <summary>
/// Raccourcis partagés par les configurations d'entités (cahier technique § 2.5).
///
/// Ce qui est commun à TOUTES les tables (clé ULID, OrganismeId, filtres, RowVersion,
/// NO ACTION, CHECK des énumérations, nommage UX_) est appliqué une seule fois dans
/// AppDbContext : les configurations ne déclarent que ce qui est propre à leur table.
/// </summary>
internal static class ConfigurationExtensions
{
    /// <summary>Pourcentage : DECIMAL(7,4).</summary>
    public static PropertyBuilder<T> EnPourcentage<T>(this PropertyBuilder<T> p) => p.HasPrecision(7, 4);

    /// <summary>Valeur d'indicateur, kilométrage, taux de change : DECIMAL(19,4).</summary>
    public static PropertyBuilder<T> EnValeur<T>(this PropertyBuilder<T> p) => p.HasPrecision(19, 4);

    /// <summary>Texte long : NVARCHAR(MAX), exclu de la borne par défaut de 400 caractères.</summary>
    public static PropertyBuilder<T> EnTexteLong<T>(this PropertyBuilder<T> p) => p.HasColumnType("nvarchar(max)");

    /// <summary>
    /// Clé étrangère vers auth.Utilisateur sans propriété de navigation (validateur,
    /// responsable, auteur...) : le modèle métier ne charge pas l'utilisateur par ce biais.
    /// </summary>
    public static void ReferenceUtilisateur<T>(this EntityTypeBuilder<T> e, Expression<Func<T, object?>> cle)
        where T : class
        => e.HasOne<Utilisateur>().WithMany().HasForeignKey(cle);
}
