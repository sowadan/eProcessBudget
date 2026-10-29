using ClosedXML.Excel;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>Historique d'une année : origine, nombre de lignes et totaux.</summary>
public sealed record AnneeHistorique(int Annee, SourceHistorique Source, int NbLignes, decimal Prevision, decimal Realise, decimal? KmEntretenus);

/// <summary>
/// Reprise de l'historique à la mise en service (réponse du métier C.2.10, décision : les
/// <see cref="AnneesReprises"/> derniers exercices avant le premier exercice géré). Import
/// Excel tout ou rien : chaque ligne porte une année et des codes (axe, rubrique, nature,
/// site) résolus dans le paramétrage ; l'import d'une année remplace ses lignes importées.
/// Les années calculées à la clôture ne sont jamais touchées.
/// </summary>
public interface IHistoriqueService
{
    /// <summary>Années d'historique, importées ou calculées à la clôture.</summary>
    Task<List<AnneeHistorique>> SyntheseAsync();

    /// <summary>Années admises à l'import (les cinq qui précèdent le premier exercice).</summary>
    Task<(int Debut, int Fin)?> AnneesAdmisesAsync();

    /// <summary>Modèle Excel à compléter : en-têtes, et les codes valides sur une seconde feuille.</summary>
    Task<byte[]?> ModeleAsync();

    Task<RapportImport> ImporterAsync(Stream fichier);

    /// <summary>Supprime les lignes importées d'une année.</summary>
    Task<(bool Ok, string? Erreur)> SupprimerAnneeAsync(int annee);
}

public sealed class HistoriqueService : IHistoriqueService
{
    /// <summary>Nombre d'exercices d'historique repris (C.2.10).</summary>
    public const int AnneesReprises = 5;

    private const string Feuille = "Historique";

    private static readonly string[] Colonnes =
    [
        "Année", "Axe", "Rubrique", "Nature de dépense", "Nature de recette", "Site",
        "Prévision", "Montant autorisé", "Montant réalisé", "Restant à payer", "Km entretenus",
    ];

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IAuditService _audit;

    public HistoriqueService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<AnneeHistorique>> SyntheseAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ExecutionsHistoriques.AsNoTracking()
            .GroupBy(h => new { h.Annee, h.Source })
            .OrderByDescending(g => g.Key.Annee).ThenBy(g => g.Key.Source)
            .Select(g => new AnneeHistorique(g.Key.Annee, g.Key.Source, g.Count(), g.Sum(h => h.Prevision), g.Sum(h => h.MontantRealise ?? 0), g.Sum(h => h.KmEntretenus)))
            .ToListAsync();
    }

    public async Task<(int Debut, int Fin)?> AnneesAdmisesAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        return await AnneesAsync(ctx);
    }

    private static async Task<(int Debut, int Fin)> AnneesAsync(AppDbContext ctx)
    {
        var premier = await ctx.Exercices.AsNoTracking().MinAsync(e => (int?)e.Annee) ?? DateTime.Today.Year;
        return (premier - AnneesReprises, premier - 1);
    }

    public async Task<byte[]?> ModeleAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var (debut, fin) = await AnneesAsync(ctx);

        using var classeur = new XLWorkbook();
        var feuille = classeur.AddWorksheet(Feuille);
        for (var i = 0; i < Colonnes.Length; i++) feuille.Cell(1, i + 1).Value = Colonnes[i];
        feuille.Row(1).Style.Font.Bold = true;
        feuille.Cell(2, 1).Value = fin;
        feuille.Columns().AdjustToContents();

        var codes = classeur.AddWorksheet("Codes");
        codes.Cell(1, 1).Value = $"Années admises : {debut} à {fin}. Une ligne de dépense porte un axe (rubrique, nature, site facultatifs) ; une ligne de recette porte une nature de recette (site facultatif).";
        void Liste(int colonne, string titre, List<(string Code, string Libelle)> valeurs)
        {
            codes.Cell(3, colonne).Value = titre;
            codes.Cell(3, colonne).Style.Font.Bold = true;
            for (var i = 0; i < valeurs.Count; i++) codes.Cell(4 + i, colonne).Value = $"{valeurs[i].Code} — {valeurs[i].Libelle}";
        }
        Liste(1, "Axes", (await ctx.Axes.AsNoTracking().Where(a => a.TypeAxe == TypeAxe.Depense).OrderBy(a => a.Ordre).Select(a => new { a.Code, a.Libelle }).ToListAsync()).Select(a => (a.Code, a.Libelle)).ToList());
        Liste(2, "Rubriques (axe : code)", (await ctx.Rubriques.AsNoTracking().OrderBy(r => r.Axe.Ordre).ThenBy(r => r.Code).Select(r => new { r.Code, r.Libelle, Axe = r.Axe.Code }).ToListAsync()).Select(r => ($"{r.Axe} : {r.Code}", r.Libelle)).ToList());
        Liste(3, "Natures de dépense", (await ctx.NaturesDepense.AsNoTracking().OrderBy(n => n.Code).Select(n => new { n.Code, n.Libelle }).ToListAsync()).Select(n => (n.Code, n.Libelle)).ToList());
        Liste(4, "Natures de recette", (await ctx.NaturesRecette.AsNoTracking().OrderBy(n => n.Code).Select(n => new { n.Code, n.Libelle }).ToListAsync()).Select(n => (n.Code, n.Libelle)).ToList());
        Liste(5, "Sites", (await ctx.Sites.AsNoTracking().OrderBy(s => s.Code).Select(s => new { s.Code, s.Nom }).ToListAsync()).Select(s => (s.Code, s.Nom)).ToList());
        codes.Columns().AdjustToContents();

        using var flux = new MemoryStream();
        classeur.SaveAs(flux);
        return flux.ToArray();
    }

    public async Task<RapportImport> ImporterAsync(Stream fichier)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return RapportImport.Refus(AccesParametrage.MessageGestion);

        XLWorkbook classeur;
        // Lecture du classeur coûteuse en calcul : hors du contexte du circuit (comme ExcelReferentiel).
        try { classeur = await Task.Run(() => new XLWorkbook(fichier)); }
        catch (Exception) { return RapportImport.Refus("Le fichier n'est pas un classeur Excel (.xlsx) lisible."); }

        using (classeur)
        {
            if (!classeur.TryGetWorksheet(Feuille, out var feuille)) return RapportImport.Refus($"La feuille « {Feuille} » est introuvable : partez du modèle.");
            for (var i = 0; i < Colonnes.Length; i++)
                if (!string.Equals(feuille.Cell(1, i + 1).GetString().Trim(), Colonnes[i], StringComparison.OrdinalIgnoreCase))
                    return RapportImport.Refus($"Colonne {i + 1} : « {Colonnes[i]} » attendue. Partez du modèle.");

            await using var ctx = await _factory.CreateDbContextAsync();
            var (debut, fin) = await AnneesAsync(ctx);
            var axes = await ctx.Axes.AsNoTracking().Where(a => a.TypeAxe == TypeAxe.Depense).ToDictionaryAsync(a => a.Code.ToUpperInvariant(), a => a.Id);
            var rubriques = await ctx.Rubriques.AsNoTracking().Select(r => new { r.Id, r.AxeId, Code = r.Code.ToUpper() }).ToListAsync();
            var naturesDepense = await ctx.NaturesDepense.AsNoTracking().ToDictionaryAsync(n => n.Code.ToUpperInvariant(), n => n.Id);
            var naturesRecette = await ctx.NaturesRecette.AsNoTracking().ToDictionaryAsync(n => n.Code.ToUpperInvariant(), n => n.Id);
            var sites = await ctx.Sites.AsNoTracking().ToDictionaryAsync(s => s.Code.ToUpperInvariant(), s => s.Id);

            var erreurs = new List<ErreurImport>();
            var lignes = new List<ExecutionHistorique>();
            var derniere = feuille.LastRowUsed()?.RowNumber() ?? 1;
            if (derniere - 1 > ExcelReferentielLimites.LignesMax) return RapportImport.Refus($"{ExcelReferentielLimites.LignesMax} lignes au plus par import.");

            for (var n = 2; n <= derniere; n++)
            {
                var ligne = feuille.Row(n);
                if (ligne.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString()))) continue;
                string Texte(int col) => ligne.Cell(col).GetString().Trim();
                void Erreur(int col, string message) => erreurs.Add(new ErreurImport(n, Colonnes[col - 1], message));
                decimal? Montant(int col, bool obligatoire = false)
                {
                    var cellule = ligne.Cell(col);
                    if (cellule.IsEmpty()) { if (obligatoire) Erreur(col, "obligatoire."); return null; }
                    if (!cellule.TryGetValue(out decimal valeur) || valeur < 0 || decimal.Round(valeur, 4) != valeur) { Erreur(col, "nombre positif attendu."); return null; }
                    return valeur;
                }
                Ulid? Code(int col, Dictionary<string, Ulid> valeurs)
                {
                    var code = Texte(col);
                    if (code.Length == 0) return null;
                    if (valeurs.TryGetValue(code.ToUpperInvariant(), out var id)) return id;
                    Erreur(col, $"code « {code} » inconnu.");
                    return null;
                }

                int annee = 0;
                if (!ligne.Cell(1).TryGetValue(out annee) || annee < debut || annee > fin) Erreur(1, $"année de {debut} à {fin} attendue (cinq exercices avant le premier exercice géré).");
                var axeId = Code(2, axes);
                var natureDepenseId = Code(4, naturesDepense);
                var natureRecetteId = Code(5, naturesRecette);
                var siteId = Code(6, sites);
                Ulid? rubriqueId = null;
                if (Texte(3) is { Length: > 0 } codeRubrique)
                {
                    var candidates = rubriques.Where(r => r.Code == codeRubrique.ToUpperInvariant() && (axeId == null || r.AxeId == axeId)).ToList();
                    if (candidates.Count == 1) rubriqueId = candidates[0].Id;
                    else Erreur(3, candidates.Count == 0 ? $"rubrique « {codeRubrique} » inconnue (pour cet axe)." : "rubrique ambiguë : précisez l'axe.");
                }
                var estRecette = natureRecetteId is not null;
                if (estRecette && (axeId is not null || natureDepenseId is not null || rubriqueId is not null))
                    Erreur(5, "une ligne de recette ne porte ni axe, ni rubrique, ni nature de dépense.");
                if (Texte(2).Length == 0 && Texte(5).Length == 0) Erreur(2,"l'axe (dépense) ou la nature de recette est obligatoire.");

                var prevision = Montant(7, obligatoire: true);
                var autorise = Montant(8);
                var realise = Montant(9);
                var restant = Montant(10);
                var km = Montant(11);
                if (erreurs.Any(e => e.Ligne == n)) continue;

                lignes.Add(new ExecutionHistorique
                {
                    Annee = annee, AxeId = axeId, RubriqueId = rubriqueId, NatureDepenseId = natureDepenseId, NatureRecetteId = natureRecetteId,
                    SiteId = siteId, Prevision = prevision ?? 0, MontantAutorise = autorise, MontantRealise = realise, RestantAPayer = restant,
                    KmEntretenus = km, Source = SourceHistorique.Import, CreePar = acteurId,
                });
            }

            if (erreurs.Count > 0) return new RapportImport(lignes.Count, 0, 0, erreurs);
            if (lignes.Count == 0) return RapportImport.Refus("Aucune ligne à importer.");

            // Une année importée remplace ses lignes importées précédentes (jamais les lignes calculées).
            var annees = lignes.Select(l => l.Annee).Distinct().ToList();
            var anciennes = await ctx.ExecutionsHistoriques.Where(h => annees.Contains(h.Annee) && h.Source == SourceHistorique.Import).ToListAsync();
            foreach (var a in anciennes)
            {
                a.EstSupprime = true;
                a.SupprimePar = acteurId;
            }
            ctx.ExecutionsHistoriques.AddRange(lignes);
            await ctx.SaveChangesAsync();

            await _audit.LoggerAsync(acteurId, null, "IMPORT", AccesParametrage.ModuleAudit, nameof(ExecutionHistorique), string.Join(",", annees.Order()),
                new { Remplacees = anciennes.Count }, new { Lignes = lignes.Count, Annees = annees.Order() });
            return new RapportImport(lignes.Count, lignes.Count, anciennes.Count, []);
        }
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAnneeAsync(int annee)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);
        await using var ctx = await _factory.CreateDbContextAsync();
        var lignes = await ctx.ExecutionsHistoriques.Where(h => h.Annee == annee && h.Source == SourceHistorique.Import).ToListAsync();
        if (lignes.Count == 0) return (false, "Aucune ligne importée pour cette année.");
        foreach (var l in lignes)
        {
            l.EstSupprime = true;
            l.SupprimePar = acteurId;
        }
        await ctx.SaveChangesAsync();
        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION_LOGIQUE", AccesParametrage.ModuleAudit, nameof(ExecutionHistorique), annee.ToString(),
            new { Lignes = lignes.Count }, null);
        return (true, null);
    }
}
