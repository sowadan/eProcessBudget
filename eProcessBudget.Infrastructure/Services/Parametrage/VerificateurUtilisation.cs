using System.Linq.Expressions;
using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>
/// « Cet élément est-il utilisé ? » (règle § 3.4 : pas de suppression logique tant que des
/// enregistrements actifs y sont rattachés).
///
/// La réponse est déduite du modèle EF Core : toutes les clés étrangères qui pointent vers
/// la table sont interrogées, y compris celles des modules à venir. Aucune liste à tenir à
/// jour. Chaque test est un EXISTS paramétré, dans l'organisme courant, lignes supprimées
/// exclues (filtres globaux).
/// </summary>
internal static class VerificateurUtilisation
{
    /// <summary>
    /// Première table qui référence l'élément, en libellé lisible ; null s'il n'est pas utilisé.
    /// </summary>
    /// <param name="actifsSeulement">
    /// true pour une désactivation : seuls comptent les référentiels rattachés encore actifs
    /// (on ne désactive pas une rubrique qui a des sous-rubriques actives). false pour une
    /// suppression : tout enregistrement rattaché compte.
    /// </param>
    /// <param name="ignorer">Tables de liaison supprimées avec l'élément.</param>
    public static async Task<string?> TrouverAsync(AppDbContext ctx, Type type, Ulid id, bool actifsSeulement,
                                                   IReadOnlyCollection<Type>? ignorer = null)
    {
        var entite = ctx.Model.FindEntityType(type)
            ?? throw new InvalidOperationException($"{type.Name} n'est pas une entité du modèle.");

        foreach (var cle in entite.GetReferencingForeignKeys().Where(c => c.Properties.Count == 1))
        {
            var dependant = cle.DeclaringEntityType.ClrType;
            if (ignorer?.Contains(dependant) == true) continue;
            if (actifsSeulement && !typeof(IActivable).IsAssignableFrom(dependant)) continue;

            var existe = (Task<bool>)typeof(VerificateurUtilisation)
                .GetMethod(nameof(ExisteAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(dependant)
                .Invoke(null, [ctx, cle.Properties[0], id, actifsSeulement])!;

            if (await existe) return Nommer(cle.DeclaringEntityType);
        }

        return null;
    }

    private static Task<bool> ExisteAsync<TDependant>(AppDbContext ctx, IProperty propriete, Ulid id, bool actifsSeulement)
        where TDependant : class
    {
        // e => e.<Fk> == id   (Ulid ou Ulid?), valeur passée en paramètre SQL
        var e = Expression.Parameter(typeof(TDependant), "e");
        // EF.Property : fonctionne aussi pour une clé étrangère sans propriété C# (shadow).
        Expression membre = Expression.Call(
            typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(propriete.ClrType),
            e, Expression.Constant(propriete.Name));
        var valeur = Expressions.Parametre(id);
        Expression corps = Expression.Equal(membre,
            membre.Type == typeof(Ulid) ? valeur : Expression.Convert(valeur, membre.Type));

        if (actifsSeulement)
            corps = Expression.AndAlso(corps, Expression.Property(e, nameof(IActivable.EstActif)));

        return ctx.Set<TDependant>().AnyAsync(Expression.Lambda<Func<TDependant, bool>>(corps, e));
    }

    /// <summary>Libellés des tables les plus courantes ; à défaut, le nom de la table.</summary>
    private static string Nommer(IEntityType type) => type.ClrType.Name switch
    {
        nameof(Structure)        => "des structures",
        nameof(Agent)            => "des agents",
        nameof(Utilisateur)      => "des comptes utilisateurs",
        nameof(Rubrique)         => "des rubriques",
        nameof(SousRubrique)     => "des sous-rubriques",
        nameof(LigneBudgetaire)  => "des lignes budgétaires",
        nameof(LigneCompte)      => "des correspondances ligne / compte",
        nameof(CompteComptable)  => "des comptes comptables",
        nameof(Tiers)            => "des tiers",
        nameof(SiteTiers)        => "des sites (collectivités)",
        nameof(NatureRecette)    => "des natures de recette",
        nameof(CompteBancaire)   => "des comptes bancaires",
        nameof(TypeTravaux)      => "des types de travaux",
        nameof(SeuilAlerte)      => "des seuils d'alerte",
        nameof(SourceFinancement) => "des sources de financement",
        nameof(Risque)           => "des risques",
        nameof(ParametreOrganisme) => "les paramètres de l'organisme",
        nameof(TauxChange)       => "des taux de change",
        _ => $"des enregistrements « {type.GetTableName() ?? type.ClrType.Name} »"
    };
}
