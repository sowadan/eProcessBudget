using ClosedXML.Excel;
using eProcessBudget.Core.Entities;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>Limites des fichiers Excel importés, partagées avec l'écran d'import.</summary>
public static class ExcelReferentielLimites
{
    public const long TailleMaxOctets = 5 * 1024 * 1024;
    public const int LignesMax = 5000;
}

/// <summary>
/// Lecture et écriture des fichiers Excel des référentiels (ClosedXML, licence MIT).
///
/// Format : une feuille de données dont la ligne 1 porte les en-têtes (un astérisque
/// signale une colonne obligatoire), et une feuille « Aide ». Le fichier exporté sert
/// directement de modèle d'import : on le complète, on le réimporte.
/// </summary>
internal static class ExcelReferentiel
{
    public const int LignesMax = ExcelReferentielLimites.LignesMax;

    public static byte[] Exporter<T>(DescripteurReferentiel<T> d, IEnumerable<T> elements,
                                     IReadOnlyDictionary<Ulid, string>? matricules = null) where T : EntiteOrganisme
    {
        using var classeur = new XLWorkbook();
        var feuille = classeur.Worksheets.Add(NomFeuille(d.Titre));

        for (var c = 0; c < d.Colonnes.Count; c++)
        {
            var colonne = d.Colonnes[c];
            var cellule = feuille.Cell(1, c + 1);
            cellule.Value = colonne.Obligatoire ? $"{colonne.Entete} *" : colonne.Entete;
            cellule.Style.Font.Bold = true;
            cellule.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3864");
            cellule.Style.Font.FontColor = XLColor.White;
        }

        var ligne = 2;
        foreach (var element in elements)
        {
            for (var c = 0; c < d.Colonnes.Count; c++)
            {
                var cellule = feuille.Cell(ligne, c + 1);
                // Texte partout : un code « 01 » ou un numéro de compte ne doit pas devenir un nombre.
                var colonne = d.Colonnes[c];
                var valeur = colonne.LireUtilisateur is { } lireId
                    ? (lireId(element) is { } id && matricules?.TryGetValue(id, out var m) == true ? m : null)
                    : colonne.Lire(element);
                cellule.Value = Convert.ToString(valeur, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                cellule.Style.NumberFormat.Format = "@";
            }
            ligne++;
        }

        feuille.SheetView.FreezeRows(1);
        feuille.Columns(1, Math.Max(1, d.Colonnes.Count)).AdjustToContents(1, Math.Min(ligne, 200));

        var aide = classeur.Worksheets.Add("Aide");
        aide.Cell(1, 1).Value = "Colonne";
        aide.Cell(1, 2).Value = "Obligatoire";
        aide.Cell(1, 3).Value = "Valeurs admises";
        aide.Row(1).Style.Font.Bold = true;
        for (var c = 0; c < d.Colonnes.Count; c++)
        {
            aide.Cell(c + 2, 1).Value = d.Colonnes[c].Entete;
            aide.Cell(c + 2, 2).Value = d.Colonnes[c].Obligatoire ? "Oui" : "Non";
            aide.Cell(c + 2, 3).Value = d.Colonnes[c].Aide ?? "";
        }
        aide.Cell(d.Colonnes.Count + 3, 1).Value =
            $"La première colonne identifie l'élément : une valeur déjà connue met à jour l'élément, une valeur nouvelle le crée. " +
            $"Si une seule ligne est en erreur, rien n'est enregistré. {LignesMax} lignes au plus.";
        aide.Columns(1, 3).AdjustToContents();

        using var flux = new MemoryStream();
        classeur.SaveAs(flux);
        return flux.ToArray();
    }

    /// <summary>
    /// Lignes de données (numéro de ligne Excel, valeurs par en-tête). Les lignes vides sont
    /// ignorées. Les erreurs de structure (en-tête manquant, fichier illisible) sont renvoyées
    /// sans lignes.
    /// </summary>
    public static (List<(int Ligne, Dictionary<string, string?> Valeurs)> Lignes, List<ErreurImport> Erreurs)
        Lire<T>(Stream fichier, DescripteurReferentiel<T> d) where T : EntiteOrganisme
    {
        var lignes = new List<(int, Dictionary<string, string?>)>();
        var erreurs = new List<ErreurImport>();

        XLWorkbook classeur;
        try
        {
            classeur = new XLWorkbook(fichier);
        }
        catch (Exception)
        {
            erreurs.Add(new(0, null, "Fichier illisible : un classeur Excel (.xlsx) est attendu."));
            return (lignes, erreurs);
        }

        using (classeur)
        {
            var feuille = classeur.Worksheets.First();
            var plage = feuille.RangeUsed();
            if (plage is null)
            {
                erreurs.Add(new(0, null, "La première feuille du classeur est vide."));
                return (lignes, erreurs);
            }

            // En-têtes : comparés sans l'astérisque d'obligation, sans casse ni espaces superflus.
            var colonnes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cellule in feuille.Row(1).CellsUsed())
                colonnes.TryAdd(cellule.GetString().Replace("*", "").Trim(), cellule.Address.ColumnNumber);

            foreach (var colonne in d.Colonnes.Where(c => c.Obligatoire && !colonnes.ContainsKey(c.Entete)))
                erreurs.Add(new(1, colonne.Entete, "colonne obligatoire absente de la ligne d'en-têtes"));
            if (erreurs.Count > 0) return (lignes, erreurs);

            var derniere = plage.LastRow().RowNumber();
            if (derniere - 1 > LignesMax)
            {
                erreurs.Add(new(0, null, $"Le fichier compte plus de {LignesMax} lignes : découpez-le."));
                return (lignes, erreurs);
            }

            for (var n = 2; n <= derniere; n++)
            {
                var valeurs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var colonne in d.Colonnes)
                    valeurs[colonne.Entete] = colonnes.TryGetValue(colonne.Entete, out var c)
                        ? feuille.Cell(n, c).GetFormattedString().Trim()
                        : null;

                if (valeurs.Values.All(string.IsNullOrWhiteSpace)) continue;
                lignes.Add((n, valeurs));
            }
        }

        if (lignes.Count == 0) erreurs.Add(new(0, null, "Aucune ligne de données sous les en-têtes."));
        return (lignes, erreurs);
    }

    /// <summary>Nom de feuille valide : 31 caractères au plus, sans caractère interdit.</summary>
    private static string NomFeuille(string titre)
    {
        var nom = new string(titre.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        return nom.Length > 31 ? nom[..31] : nom.Length == 0 ? "Données" : nom;
    }
}
