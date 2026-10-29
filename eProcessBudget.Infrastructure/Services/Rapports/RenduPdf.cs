using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace eProcessBudget.Infrastructure.Services.Rapports;

/// <summary>
/// Rendu PDF d'un état (QuestPDF ; cahier fonctionnel § 3.12.3) : A4 portrait ou paysage,
/// en-tête institutionnel (logo, raison sociale, en-tête paramétré, titre, filtres) et pied de
/// page (pied paramétré, date d'édition, pagination) sur chaque page.
/// </summary>
internal static class RenduPdf
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");
    private const string Marine = "#1F3864";
    private const string FondFort = "#EEF2F8";

    public static byte[] Generer(Rapport rapport, EnteteInstitutionnelle entete)
    {
        var edition = DateTime.Now;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(rapport.Paysage ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.Margin(1.4f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(8.5f));

            page.Header().PaddingBottom(8).Column(col =>
            {
                col.Item().Row(row =>
                {
                    if (entete.Logo is { Length: > 0 } logo) row.ConstantItem(55).Height(40).Image(logo).FitArea();
                    row.RelativeItem().PaddingLeft(entete.Logo is { Length: > 0 } ? 8 : 0).Column(c =>
                    {
                        c.Item().Text(entete.RaisonSociale + (entete.Sigle is null ? "" : $" ({entete.Sigle})")).Bold().FontSize(10).FontColor(Marine);
                        if (!string.IsNullOrWhiteSpace(entete.EnteteRapport)) c.Item().Text(entete.EnteteRapport).FontSize(8).FontColor(Colors.Grey.Darken1);
                        if (entete.Adresse is not null || entete.Contact is not null)
                            c.Item().Text(string.Join(" · ", new[] { entete.Adresse, entete.Contact }.Where(x => !string.IsNullOrWhiteSpace(x)))).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    });
                });
                col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Marine);
                col.Item().PaddingTop(6).Text(rapport.Titre).Bold().FontSize(13);
                if (rapport.SousTitre is not null) col.Item().Text(rapport.SousTitre).FontColor(Colors.Grey.Darken2);
                if (rapport.Filtres.Count > 0)
                    col.Item().PaddingTop(2).Text(string.Join("   ·   ", rapport.Filtres.Select(f => $"{f.Libelle} : {f.Valeur}"))).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            });

            page.Content().Column(col =>
            {
                col.Spacing(10);
                foreach (var section in rapport.Sections) Section(col.Item(), section);
            });

            page.Footer().BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Text($"{(string.IsNullOrWhiteSpace(entete.PiedPageRapport) ? entete.RaisonSociale : entete.PiedPageRapport)} · montants en {entete.Devise} · édité le {edition:dd/MM/yyyy à HH:mm}")
                   .FontSize(7).FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Darken1));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    private static void Section(IContainer conteneur, SectionRapport section) => conteneur.Column(col =>
    {
        col.Item().Text(section.Titre).Bold().FontSize(10.5f).FontColor(Marine);
        if (section.Chiffres is { Count: > 0 } chiffres)
            col.Item().PaddingTop(4).Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(1.5f); });
                foreach (var (libelle, valeur) in chiffres)
                {
                    t.Cell().PaddingVertical(1.5f).Text(libelle).FontColor(Colors.Grey.Darken2);
                    t.Cell().PaddingVertical(1.5f).AlignRight().Text(valeur).Bold();
                }
            });
        if (!string.IsNullOrWhiteSpace(section.Texte))
            col.Item().PaddingTop(4).Text(section.Texte).LineHeight(1.3f);
        if (section.Tableau is { } tableau)
            col.Item().PaddingTop(4).Element(c => Tableau(c, tableau));
    });

    private static void Tableau(IContainer conteneur, TableauRapport tableau) => conteneur.Table(t =>
    {
        t.ColumnsDefinition(c =>
        {
            foreach (var colonne in tableau.Colonnes) c.RelativeColumn(colonne.Largeur);
        });
        t.Header(h =>
        {
            foreach (var colonne in tableau.Colonnes)
            {
                var cellule = h.Cell().Background(Marine).PaddingVertical(3).PaddingHorizontal(3);
                (colonne.Type == TypeColonne.Texte ? cellule : cellule.AlignRight()).Text(colonne.Entete).FontColor(Colors.White).Bold().FontSize(7.5f);
            }
        });
        foreach (var ligne in tableau.Lignes) Ligne(t, tableau, ligne);
        if (tableau.Total is { } total) Ligne(t, tableau, total with { Forte = true });
    });

    private static void Ligne(TableDescriptor t, TableauRapport tableau, LigneRapport ligne)
    {
        for (var c = 0; c < tableau.Colonnes.Count; c++)
        {
            var colonne = tableau.Colonnes[c];
            var cellule = t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2).PaddingHorizontal(3);
            if (ligne.Forte) cellule = cellule.Background(FondFort);
            if (c == 0 && ligne.Niveau > 0) cellule = cellule.PaddingLeft(3 + 8 * ligne.Niveau);
            if (colonne.Type != TypeColonne.Texte) cellule = cellule.AlignRight();
            var texte = cellule.Text(Formater(ligne.Cellules.ElementAtOrDefault(c), colonne.Type));
            if (ligne.Forte) texte.Bold();
        }
    }

    /// <summary>Valeur affichée d'une cellule, au format français.</summary>
    internal static string Formater(object? valeur, TypeColonne type) => valeur switch
    {
        null => type == TypeColonne.Taux ? "n.s." : "",
        decimal d when type == TypeColonne.Taux => d.ToString("N2", Francais) + " %",
        decimal d when type == TypeColonne.Nombre => d.ToString("#,##0.####", Francais),
        decimal d => d.ToString("N2", Francais),
        int i => i.ToString("N0", Francais),
        DateOnly date => date.ToString("dd/MM/yyyy"),
        DateTime date => date.ToString("dd/MM/yyyy HH:mm"),
        _ => Convert.ToString(valeur, Francais) ?? "",
    };
}
