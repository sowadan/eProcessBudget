namespace eProcessBudget.Infrastructure.Services.Rapports;

/// <summary>Nature d'une colonne : elle fixe l'alignement et le format, en Excel comme en PDF.</summary>
public enum TypeColonne
{
    Texte,

    /// <summary>Montant en devise, 2 décimales ; total par formule en Excel.</summary>
    Montant,

    /// <summary>Taux en pourcentage (54,17 = 54,17 %) ; null affiché « n.s. ».</summary>
    Taux,

    Nombre,
}

/// <param name="Largeur">Largeur relative en PDF (1 = largeur courante).</param>
public sealed record ColonneRapport(string Entete, TypeColonne Type = TypeColonne.Texte, float Largeur = 1);

/// <summary>Une ligne de tableau ; <see cref="Niveau"/> > 0 la met en retrait (détail d'un sous-total).</summary>
public sealed record LigneRapport(IReadOnlyList<object?> Cellules, int Niveau = 0, bool Forte = false);

/// <summary>Tableau d'un état : colonnes, lignes et, le cas échéant, ligne de total.</summary>
public sealed record TableauRapport(IReadOnlyList<ColonneRapport> Colonnes, IReadOnlyList<LigneRapport> Lignes, LigneRapport? Total = null)
{
    /// <summary>Lignes toutes au même niveau : le total Excel peut alors être une formule SOMME.</summary>
    public bool EstPlat => Lignes.All(l => l.Niveau == 0 && !l.Forte);
}

/// <summary>Une section d'état : un titre, puis des chiffres clés, un texte et/ou un tableau.</summary>
public sealed record SectionRapport(
    string Titre, TableauRapport? Tableau = null, string? Texte = null, IReadOnlyList<(string Libelle, string Valeur)>? Chiffres = null);

/// <summary>
/// Modèle d'un état (cahier technique § 2.10) : produit une fois, rendu en Excel et en PDF.
/// Les filtres appliqués figurent en en-tête de chaque export (cahier fonctionnel § 4.2).
/// </summary>
public sealed record Rapport(
    string Titre, string? SousTitre, IReadOnlyList<(string Libelle, string Valeur)> Filtres, IReadOnlyList<SectionRapport> Sections,
    bool Paysage = false);

/// <summary>Identité de l'organisme reprise en en-tête et pied de page des exports (§ 3.2.2).</summary>
public sealed record EnteteInstitutionnelle(
    string RaisonSociale, string? Sigle, string? Adresse, string? Contact, byte[]? Logo, string? EnteteRapport, string? PiedPageRapport, string Devise);

/// <summary>Fichier produit par un export.</summary>
public sealed record FichierRapport(byte[] Contenu, string NomFichier, string ContentType);

public enum FormatRapport
{
    Excel = 1,
    Pdf = 2,
}

/// <summary>Raccourcis de construction des tableaux d'état.</summary>
internal static class Lignes
{
    public static LigneRapport L(params object?[] cellules) => new(cellules);
    public static LigneRapport Detail(params object?[] cellules) => new(cellules, Niveau: 1);
    public static LigneRapport Fort(params object?[] cellules) => new(cellules, Forte: true);

    public static ColonneRapport Texte(string entete, float largeur = 2) => new(entete, TypeColonne.Texte, largeur);
    public static ColonneRapport Montant(string entete) => new(entete, TypeColonne.Montant, 1.2f);
    public static ColonneRapport Taux(string entete) => new(entete, TypeColonne.Taux, 0.8f);
    public static ColonneRapport Nombre(string entete) => new(entete, TypeColonne.Nombre, 0.8f);
}
