using System.Text.Json;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Rapports;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Audit;

/// <summary>Filtres multicritères du journal d'audit (cahier technique § 4.9).</summary>
public sealed class FiltreAudit
{
    public DateOnly? Du { get; set; }
    public DateOnly? Au { get; set; }
    public Ulid? UtilisateurId { get; set; }
    public string? Module { get; set; }
    public string? Action { get; set; }

    /// <summary>Entité (table) concernée.</summary>
    public string? Entite { get; set; }

    /// <summary>Identifiant de l'élément, matricule, adresse IP ou commentaire.</summary>
    public string? Recherche { get; set; }

    public bool? EstSucces { get; set; }

    /// <summary>true : écritures journalisées automatiquement (module « Données ») ; false : événements métier ; null : tout.</summary>
    public bool? Automatique { get; set; }
}

public sealed record LigneJournal(
    Ulid Id, DateTime DateAction, Ulid? UtilisateurId, string? Matricule, string Module, string Action, string Entite, string? EntiteId,
    string? AdresseIp, bool EstSucces, bool AvecValeurs);

/// <summary>Un champ et ses valeurs avant / après, pour l'affichage côte à côte.</summary>
public sealed record ChampAudit(string Champ, string? Avant, string? Apres)
{
    public bool EstModifie => Avant != Apres;
}

public sealed record DetailJournal(
    LigneJournal Ligne, string? Utilisateur, string? UserAgent, string? Commentaire, string? MessageErreur, string? CorrelationId,
    List<ChampAudit> Champs, string? AvantBrut, string? ApresBrut);

/// <summary>
/// Consultation et export du journal d'audit (cahier fonctionnel § 3.13, technique § 4.9) :
/// filtres multicritères, valeurs avant / après côte à côte, export Excel ou PDF. Lecture :
/// AuditConsultation ; export : AuditExportation. Un utilisateur d'organisme ne voit que les
/// lignes de son organisme, un Administrateur plateforme celles du niveau plateforme.
/// </summary>
public interface IJournalAuditService
{
    Task<(List<LigneJournal> Items, int Total)> RechercherAsync(FiltreAudit filtre, int page = 1, int taille = 50);
    Task<DetailJournal?> ObtenirAsync(Ulid id);

    /// <summary>Valeurs proposées par les filtres : modules, actions, entités présents au journal.</summary>
    Task<(List<string> Modules, List<string> Actions, List<string> Entites)> ListerValeursAsync();

    /// <summary>Utilisateurs de l'organisme (ou de la plateforme), pour le filtre.</summary>
    Task<List<ElementListe>> ListerUtilisateursAsync();

    /// <summary>Durée de rétention des journaux de l'organisme, en mois.</summary>
    Task<int> RetentionMoisAsync();

    Task<(bool Ok, string? Erreur, FichierRapport? Fichier)> ExporterAsync(FiltreAudit filtre, FormatRapport format);
}

public sealed class JournalAuditService : IJournalAuditService
{
    /// <summary>Plafond d'un export (les plus récentes d'abord).</summary>
    public const int ExportMax = 10_000;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IUserContextService _utilisateur;
    private readonly IDroitService _droits;
    private readonly IAuditService _audit;

    public JournalAuditService(IDbContextFactory<AppDbContext> factory, IUserContextService utilisateur, IDroitService droits, IAuditService audit)
    {
        _factory = factory;
        _utilisateur = utilisateur;
        _droits = droits;
        _audit = audit;
    }

    public async Task<(List<LigneJournal> Items, int Total)> RechercherAsync(FiltreAudit f, int page = 1, int taille = 50)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await VisibleAsync(ctx, TypeDroit.AuditConsultation) is not { } q) return ([], 0);
        q = Filtrer(q, f);
        var total = await q.CountAsync();
        var items = await Projeter(q.OrderByDescending(a => a.DateAction).ThenByDescending(a => a.Id)
            .Skip(Math.Max(0, (page - 1) * taille)).Take(Math.Clamp(taille, 1, 200))).ToListAsync();
        return (items, total);
    }

    public async Task<DetailJournal?> ObtenirAsync(Ulid id)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await VisibleAsync(ctx, TypeDroit.AuditConsultation) is not { } q) return null;
        var a = await q.FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return null;
        var nom = a.UtilisateurId is { } u
            ? await ctx.Utilisateurs.AsNoTracking().Where(x => x.Id == u).Select(x => x.NomPrenoms).FirstOrDefaultAsync()
            : null;
        return new DetailJournal(VersLigne(a), nom, a.UserAgent, a.Commentaire, a.MessageErreur, a.CorrelationId,
            Comparer(a.AncienneValeur, a.NouvelleValeur), a.AncienneValeur, a.NouvelleValeur);
    }

    public async Task<(List<string> Modules, List<string> Actions, List<string> Entites)> ListerValeursAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await VisibleAsync(ctx, TypeDroit.AuditConsultation) is not { } q) return ([], [], []);
        return (await q.Select(a => a.Module).Distinct().OrderBy(x => x).ToListAsync(),
                await q.Select(a => a.Action).Distinct().OrderBy(x => x).ToListAsync(),
                await q.Select(a => a.Entite).Distinct().OrderBy(x => x).ToListAsync());
    }

    public async Task<List<ElementListe>> ListerUtilisateursAsync()
    {
        var userId = await _utilisateur.ObtenirUserIdAsync();
        if (!await _droits.ADroitAsync(userId, TypeDroit.AuditConsultation)) return [];
        var organisme = await Perimetre();
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.Utilisateurs.AsNoTracking().Where(u => u.OrganismeId == organisme)
            .OrderBy(u => u.Matricule).Select(u => new ElementListe(u.Id, u.Matricule + " — " + u.NomPrenoms)).ToListAsync();
    }

    public async Task<int> RetentionMoisAsync()
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.ParametresOrganisme.AsNoTracking().Select(p => (int?)p.RetentionJournauxMois).FirstOrDefaultAsync() ?? PurgeJournaux.RetentionMinimaleMois;
    }

    public async Task<(bool Ok, string? Erreur, FichierRapport? Fichier)> ExporterAsync(FiltreAudit f, FormatRapport format)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        if (await VisibleAsync(ctx, TypeDroit.AuditExportation) is not { } q) return (false, "Vous n'avez pas le droit d'exporter le journal d'audit.", null);
        if (!await _droits.ADroitAsync(await _utilisateur.ObtenirUserIdAsync(), TypeDroit.AuditConsultation))
            return (false, "Vous n'avez pas le droit de consulter le journal d'audit.", null);
        if (f.Du is { } du && f.Au is { } au && au < du) return (false, "La date de fin précède la date de début.", null);

        q = Filtrer(q, f);
        var total = await q.CountAsync();
        var lignes = await q.OrderByDescending(a => a.DateAction).ThenByDescending(a => a.Id).Take(ExportMax)
            .Select(a => new { a.DateAction, a.UtilisateurMatricule, a.AdresseIP, a.Module, a.Action, a.Entite, a.EntiteId, a.EstSucces, a.AncienneValeur, a.NouvelleValeur, a.Commentaire })
            .ToListAsync();

        // Le PDF reste lisible : valeurs abrégées ; l'Excel les garde entières.
        var limite = format == FormatRapport.Pdf ? 300 : 30_000;
        static string? Abreger(string? texte, int max) => texte is null || texte.Length <= max ? texte : texte[..max] + "…";
        var tableau = new TableauRapport(
        [
            new("Date", TypeColonne.Texte, 1.3f), new("Utilisateur", TypeColonne.Texte, 1), new("Adresse IP", TypeColonne.Texte, 1),
            new("Module", TypeColonne.Texte, 1), new("Action", TypeColonne.Texte, 1.2f), new("Élément", TypeColonne.Texte, 1.3f),
            new("Identifiant", TypeColonne.Texte, 1.6f), new("Résultat", TypeColonne.Texte, 0.7f),
            new("Valeurs avant", TypeColonne.Texte, 2.5f), new("Valeurs après", TypeColonne.Texte, 2.5f),
        ],
        lignes.Select(a => new LigneRapport([a.DateAction.ToLocalTime(), a.UtilisateurMatricule ?? "système", a.AdresseIP, a.Module, a.Action, a.Entite, a.EntiteId,
            a.EstSucces ? "Succès" : "Échec", Abreger(a.AncienneValeur, limite), Abreger(a.NouvelleValeur ?? a.Commentaire, limite)])).ToList());

        var filtres = await DecrireAsync(ctx, f);
        var rapport = new Rapport("Journal d'audit", total > ExportMax ? $"{ExportMax:N0} lignes les plus récentes sur {total:N0}" : $"{total:N0} ligne(s)",
            filtres, [new("Opérations journalisées", tableau)], Paysage: true);
        var entete = await RapportService.EnteteAsync(ctx);
        // Rendu coûteux en calcul (jusqu'à ExportMax lignes) : hors du contexte du circuit.
        var (contenu, extension, type) = format == FormatRapport.Excel
            ? (await Task.Run(() => RenduExcel.Generer(rapport, entete)), "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            : (await Task.Run(() => RenduPdf.Generer(rapport, entete)), "pdf", "application/pdf");

        await _audit.LoggerAsync(await _utilisateur.ObtenirUserIdAsync(), null, "EXPORT", "Audit", "JournalAudit", null, null,
            new { Format = format.ToString(), Lignes = lignes.Count, Filtres = filtres.Select(x => $"{x.Libelle} : {x.Valeur}").ToList() });
        return (true, null, new FichierRapport(contenu, $"journal_audit_{DateTime.Now:yyyyMMdd_HHmm}.{extension}", type));
    }

    // ═════ Outils ═════════════════════════════════════════════════════════════

    /// <summary>Organisme de l'utilisateur, ou null pour le niveau plateforme.</summary>
    private async Task<Ulid?> Perimetre()
    {
        var organisme = await _utilisateur.ObtenirOrganismeIdAsync();
        return organisme == Ulid.Empty ? null : organisme;
    }

    /// <summary>Lignes que l'utilisateur peut consulter avec ce droit, ou null.</summary>
    private async Task<IQueryable<AuditLog>?> VisibleAsync(AppDbContext ctx, TypeDroit droit)
    {
        if (!await _droits.ADroitAsync(await _utilisateur.ObtenirUserIdAsync(), droit)) return null;
        var organisme = await Perimetre();
        return ctx.AuditLogs.AsNoTracking().Where(a => a.OrganismeId == organisme);
    }

    internal static IQueryable<AuditLog> Filtrer(IQueryable<AuditLog> q, FiltreAudit f)
    {
        // Les dates du filtre sont des jours locaux ; le journal est en UTC.
        if (f.Du is { } du) { var debut = du.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime(); q = q.Where(a => a.DateAction >= debut); }
        if (f.Au is { } au) { var fin = au.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime(); q = q.Where(a => a.DateAction < fin); }
        if (f.UtilisateurId is { } u) q = q.Where(a => a.UtilisateurId == u);
        if (!string.IsNullOrWhiteSpace(f.Module)) q = q.Where(a => a.Module == f.Module);
        if (!string.IsNullOrWhiteSpace(f.Action)) q = q.Where(a => a.Action == f.Action);
        if (!string.IsNullOrWhiteSpace(f.Entite)) q = q.Where(a => a.Entite == f.Entite);
        if (f.EstSucces is { } succes) q = q.Where(a => a.EstSucces == succes);
        if (f.Automatique is { } auto) q = auto ? q.Where(a => a.Module == JournalisationAutomatique.Module) : q.Where(a => a.Module != JournalisationAutomatique.Module);
        if (!string.IsNullOrWhiteSpace(f.Recherche))
        {
            var r = f.Recherche.Trim();
            q = q.Where(a => (a.EntiteId != null && a.EntiteId.Contains(r)) || (a.UtilisateurMatricule != null && a.UtilisateurMatricule.Contains(r))
                          || (a.AdresseIP != null && a.AdresseIP.Contains(r)) || (a.Commentaire != null && a.Commentaire.Contains(r)));
        }
        return q;
    }

    private static IQueryable<LigneJournal> Projeter(IQueryable<AuditLog> q)
        => q.Select(a => new LigneJournal(a.Id, a.DateAction, a.UtilisateurId, a.UtilisateurMatricule, a.Module, a.Action, a.Entite, a.EntiteId,
            a.AdresseIP, a.EstSucces, a.AncienneValeur != null || a.NouvelleValeur != null));

    private static LigneJournal VersLigne(AuditLog a) => new(a.Id, a.DateAction, a.UtilisateurId, a.UtilisateurMatricule, a.Module, a.Action, a.Entite,
        a.EntiteId, a.AdresseIP, a.EstSucces, a.AncienneValeur != null || a.NouvelleValeur != null);

    /// <summary>
    /// Valeurs avant / après champ par champ quand ce sont des objets JSON (ordre : champs
    /// d'avant, puis nouveaux champs) ; sinon une seule ligne « Valeur ».
    /// </summary>
    internal static List<ChampAudit> Comparer(string? avant, string? apres)
    {
        var a = Champs(avant);
        var b = Champs(apres);
        if (a is null && b is null)
            return avant is null && apres is null ? [] : [new ChampAudit("Valeur", avant, apres)];
        a ??= [];
        b ??= [];
        return a.Keys.Concat(b.Keys.Where(k => !a.ContainsKey(k)))
            .Select(k => new ChampAudit(k, a.GetValueOrDefault(k), b.GetValueOrDefault(k))).ToList();
    }

    private static Dictionary<string, string?>? Champs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Null => null,
                _ => p.Value.GetRawText(),
            });
        }
        catch (JsonException) { return null; }
    }

    /// <summary>Filtres en clair, pour l'en-tête de l'export.</summary>
    private static async Task<List<(string Libelle, string Valeur)>> DecrireAsync(AppDbContext ctx, FiltreAudit f)
    {
        var filtres = new List<(string, string)>();
        if (f.Du is not null || f.Au is not null) filtres.Add(("Période", $"{f.Du?.ToString("dd/MM/yyyy") ?? "…"} au {f.Au?.ToString("dd/MM/yyyy") ?? "…"}"));
        if (f.UtilisateurId is { } u)
            filtres.Add(("Utilisateur", await ctx.Utilisateurs.AsNoTracking().Where(x => x.Id == u).Select(x => x.Matricule).FirstOrDefaultAsync() ?? u.ToString()));
        if (!string.IsNullOrWhiteSpace(f.Module)) filtres.Add(("Module", f.Module));
        if (!string.IsNullOrWhiteSpace(f.Action)) filtres.Add(("Action", f.Action));
        if (!string.IsNullOrWhiteSpace(f.Entite)) filtres.Add(("Élément", f.Entite));
        if (f.EstSucces is { } s) filtres.Add(("Résultat", s ? "Succès" : "Échec"));
        if (f.Automatique is { } auto) filtres.Add(("Type", auto ? "Écritures (automatique)" : "Événements métier"));
        if (!string.IsNullOrWhiteSpace(f.Recherche)) filtres.Add(("Recherche", f.Recherche.Trim()));
        return filtres;
    }
}
