using ClosedXML.Excel;

namespace eProcessBudget.Infrastructure.Services.Rapports;

/// <summary>
/// Rendu Excel d'un état (ClosedXML, licence MIT ; cahier fonctionnel § 3.12.3) : une première
/// feuille « Synthèse » (identité de l'organisme, titre, filtres, chiffres clés et textes), puis
/// une feuille par tableau, mis en forme, avec des totaux par formule quand les lignes sont à
/// plat.
/// </summary>
internal static class RenduExcel
{
    private const string FormatMontant = "#,##0.00";
    private const string FormatTaux = "0.00\" %\"";
    private const string FormatNombre = "#,##0.####";
    private static readonly XLColor Marine = XLColor.FromHtml("#1F3864");
    private static readonly XLColor FondFort = XLColor.FromHtml("#EEF2F8");

    public static byte[] Generer(Rapport rapport, EnteteInstitutionnelle entete)
    {
        using var classeur = new XLWorkbook();
        classeur.Properties.Title = rapport.Titre;
        classeur.Properties.Company = entete.RaisonSociale;

        // Noms des feuilles de tableau, fixés d'avance pour que la synthèse y renvoie.
        var utilises = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Synthèse" };
        var noms = rapport.Sections.Select(s => s.Tableau is null ? null : NomFeuille(s.Titre, utilises)).ToList();

        var synthese = classeur.Worksheets.Add("Synthèse");
        var ligne = EcrireEnTete(synthese, rapport, entete, rapport.Titre);
        for (var i = 0; i < rapport.Sections.Count; i++)
        {
            var section = rapport.Sections[i];
            ligne++;
            synthese.Cell(ligne, 1).Value = section.Titre;
            synthese.Cell(ligne, 1).Style.Font.SetBold().Font.SetFontSize(12).Font.SetFontColor(Marine);
            ligne++;
            foreach (var (libelle, valeur) in section.Chiffres ?? [])
            {
                synthese.Cell(ligne, 1).Value = libelle;
                synthese.Cell(ligne, 2).Value = valeur;
                synthese.Cell(ligne, 2).Style.Font.SetBold();
                ligne++;
            }
            if (!string.IsNullOrWhiteSpace(section.Texte))
            {
                synthese.Cell(ligne, 1).Value = section.Texte;
                synthese.Range(ligne, 1, ligne, 6).Merge().Style.Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
                synthese.Row(ligne).Height = Math.Min(400, 15 * (1 + section.Texte.Length / 90));
                ligne++;
            }
            if (section.Tableau is not null)
            {
                synthese.Cell(ligne, 1).Value = $"Voir la feuille « {noms[i]} ».";
                synthese.Cell(ligne, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
                ligne++;
            }
        }
        synthese.Column(1).Width = 45;
        synthese.Column(2).Width = 30;

        for (var i = 0; i < rapport.Sections.Count; i++)
        {
            var section = rapport.Sections[i];
            if (section.Tableau is null) continue;
            var feuille = classeur.Worksheets.Add(noms[i]!);
            var debut = EcrireEnTete(feuille, rapport, entete, section.Titre) + 1;
            EcrireTableau(feuille, section.Tableau!, debut);
            feuille.PageSetup.PageOrientation = rapport.Paysage ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
            feuille.PageSetup.FitToPages(1, 0);
        }

        using var flux = new MemoryStream();
        classeur.SaveAs(flux);
        return flux.ToArray();
    }

    /// <summary>Identité de l'organisme, titre et filtres ; renvoie la dernière ligne écrite.</summary>
    private static int EcrireEnTete(IXLWorksheet feuille, Rapport rapport, EnteteInstitutionnelle entete, string titre)
    {
        var ligne = 1;
        feuille.Cell(ligne, 1).Value = entete.RaisonSociale + (entete.Sigle is null ? "" : $" ({entete.Sigle})");
        feuille.Cell(ligne, 1).Style.Font.SetBold().Font.SetFontColor(Marine);
        if (!string.IsNullOrWhiteSpace(entete.EnteteRapport)) feuille.Cell(++ligne, 1).Value = entete.EnteteRapport;
        ligne += 2;
        feuille.Cell(ligne, 1).Value = titre;
        feuille.Cell(ligne, 1).Style.Font.SetBold().Font.SetFontSize(14);
        if (titre != rapport.Titre) feuille.Cell(++ligne, 1).Value = rapport.Titre;
        if (rapport.SousTitre is not null) feuille.Cell(++ligne, 1).Value = rapport.SousTitre;
        foreach (var (libelle, valeur) in rapport.Filtres)
        {
            ligne++;
            feuille.Cell(ligne, 1).Value = $"{libelle} : {valeur}";
            feuille.Cell(ligne, 1).Style.Font.SetFontColor(XLColor.Gray);
        }
        feuille.Cell(++ligne, 1).Value = $"Montants en {entete.Devise} · édité le {DateTime.Now:dd/MM/yyyy à HH:mm}";
        feuille.Cell(ligne, 1).Style.Font.SetFontColor(XLColor.Gray).Font.SetItalic();
        return ligne + 1;
    }

    private static void EcrireTableau(IXLWorksheet feuille, TableauRapport tableau, int ligneEntete)
    {
        for (var c = 0; c < tableau.Colonnes.Count; c++)
        {
            var cellule = feuille.Cell(ligneEntete, c + 1);
            cellule.Value = tableau.Colonnes[c].Entete;
            cellule.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(Marine)
                   .Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            if (tableau.Colonnes[c].Type != TypeColonne.Texte) cellule.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }

        var ligne = ligneEntete + 1;
        foreach (var l in tableau.Lignes)
        {
            EcrireLigne(feuille, tableau, l, ligne);
            ligne++;
        }
        var derniere = ligne - 1;

        if (tableau.Total is { } total)
        {
            EcrireLigne(feuille, tableau, total with { Forte = true }, ligne);
            if (tableau.EstPlat && derniere > ligneEntete)
                for (var c = 0; c < tableau.Colonnes.Count; c++)
                    if (tableau.Colonnes[c].Type is TypeColonne.Montant or TypeColonne.Nombre && total.Cellules.ElementAtOrDefault(c) is not null and not string)
                    {
                        var colonne = feuille.Column(c + 1).ColumnLetter();
                        feuille.Cell(ligne, c + 1).FormulaA1 = $"SUM({colonne}{ligneEntete + 1}:{colonne}{derniere})";
                    }
            feuille.Row(ligne).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);
        }

        feuille.SheetView.FreezeRows(ligneEntete);
        for (var c = 0; c < tableau.Colonnes.Count; c++)
            feuille.Column(c + 1).Width = tableau.Colonnes[c].Type == TypeColonne.Texte ? Math.Clamp(14 * tableau.Colonnes[c].Largeur, 14, 60) : 16;
        feuille.Row(ligneEntete).Height = 30;
    }

    private static void EcrireLigne(IXLWorksheet feuille, TableauRapport tableau, LigneRapport l, int ligne)
    {
        for (var c = 0; c < tableau.Colonnes.Count; c++)
        {
            var cellule = feuille.Cell(ligne, c + 1);
            var valeur = l.Cellules.ElementAtOrDefault(c);
            var type = tableau.Colonnes[c].Type;
            switch (valeur)
            {
                case null when type == TypeColonne.Taux:
                    cellule.Value = "n.s.";
                    cellule.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                    break;
                case null:
                    break;
                case decimal d:
                    cellule.Value = d;
                    cellule.Style.NumberFormat.Format = type switch { TypeColonne.Taux => FormatTaux, TypeColonne.Nombre => FormatNombre, _ => FormatMontant };
                    break;
                case int i:
                    cellule.Value = i;
                    break;
                default:
                    // Texte : un code « 01 » ou un numéro de compte ne doit pas devenir un nombre.
                    cellule.Value = Convert.ToString(valeur, System.Globalization.CultureInfo.GetCultureInfo("fr-FR")) ?? "";
                    cellule.Style.NumberFormat.Format = "@";
                    break;
            }
        }
        if (l.Niveau > 0) feuille.Cell(ligne, 1).Style.Alignment.SetIndent(l.Niveau * 2);
        if (l.Forte) feuille.Range(ligne, 1, ligne, tableau.Colonnes.Count).Style.Font.SetBold().Fill.SetBackgroundColor(FondFort);
    }

    /// <summary>Nom de feuille Excel valide et unique (31 caractères, sans : \ / ? * [ ]).</summary>
    private static string NomFeuille(string titre, HashSet<string> utilises)
    {
        var nettoye = new string(titre.Select(ch => @":\/?*[]".Contains(ch) ? ' ' : ch).ToArray()).Trim();
        var nom = nettoye.Length > 28 ? nettoye[..28].Trim() : nettoye;
        if (nom.Length == 0) nom = "Tableau";
        var candidat = nom;
        for (var i = 2; !utilises.Add(candidat); i++) candidat = $"{nom} {i}";
        return candidat;
    }
}
