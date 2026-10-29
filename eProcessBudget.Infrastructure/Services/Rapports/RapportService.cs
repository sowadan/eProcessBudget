using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Parametrage;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace eProcessBudget.Infrastructure.Services.Rapports;

/// <summary>
/// Rapports et exportation (cahier fonctionnel § 3.12, technique § 2.10 et 4.8) : chaque état
/// produit un modèle unique, rendu en Excel (ClosedXML) ou en PDF (QuestPDF), avec en-tête
/// institutionnel et filtres appliqués. Chaque export est journalisé.
/// </summary>
public interface IRapportService
{
    /// <summary>États que l'utilisateur peut produire : RapportExporter et un droit de lecture de leurs données.</summary>
    Task<List<DescripteurRapport>> ListerDisponiblesAsync();

    Task<(bool Ok, string? Erreur, FichierRapport? Fichier)> GenererAsync(string code, ParametresRapport parametres, FormatRapport format);
}

public sealed class RapportService : IRapportService
{
    public const string ModuleAudit = "Rapports";

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IDroitService _droits;
    private readonly GenerateurRapports _generateur;
    private readonly IAuditService _audit;

    public RapportService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IDroitService droits,
                          GenerateurRapports generateur, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _droits = droits;
        _generateur = generateur;
        _audit = audit;
    }

    public async Task<List<DescripteurRapport>> ListerDisponiblesAsync()
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.RapportExporter);
        if (acteurId == Ulid.Empty) return [];
        var droits = await _droits.ObtenirDroitsAsync(acteurId);
        return CatalogueRapports.Tous.Where(r => r.Droits.Any(droits.Contains)).ToList();
    }

    public async Task<(bool Ok, string? Erreur, FichierRapport? Fichier)> GenererAsync(string code, ParametresRapport p, FormatRapport format)
    {
        if (CatalogueRapports.Obtenir(code) is not { } d) return (false, "État inconnu.", null);
        var acteurId = await _acces.ActeurAsync(TypeDroit.RapportExporter);
        if (acteurId == Ulid.Empty) return (false, "Vous n'avez pas le droit d'exporter des rapports.", null);
        if (!await _droits.ADroitAsync(acteurId, d.Droits)) return (false, "Vous n'avez pas accès aux données de cet état.", null);
        if (!Enum.IsDefined(format)) return (false, "Format inconnu.", null);

        await using var ctx = await _factory.CreateDbContextAsync();
        var erreur = await ControlerAsync(ctx, d, p);
        if (erreur is not null) return (false, erreur, null);

        var filtres = await FiltresAsync(ctx, p);
        var rapport = new Rapport(d.Titre, null, filtres, await _generateur.GenererAsync(d, p), d.Paysage);
        var entete = await EnteteAsync(ctx);
        // Rendu coûteux en calcul : hors du contexte du circuit.
        var (contenu, extension, type) = format == FormatRapport.Excel
            ? (await Task.Run(() => RenduExcel.Generer(rapport, entete)), "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
            : (await Task.Run(() => RenduPdf.Generer(rapport, entete)), "pdf", "application/pdf");

        await _audit.LoggerAsync(acteurId, null, "EXPORT", ModuleAudit, "Rapport", d.Code, null,
            new { Format = format.ToString(), Filtres = filtres.Select(f => $"{f.Libelle} : {f.Valeur}").ToList() });
        return (true, null, new FichierRapport(contenu, $"{d.Code.ToLowerInvariant()}_{DateTime.Now:yyyyMMdd_HHmm}.{extension}", type));
    }

    /// <summary>Les paramètres demandés par l'état sont fournis et cohérents.</summary>
    internal static async Task<string?> ControlerAsync(AppDbContext ctx, DescripteurRapport d, ParametresRapport p)
    {
        var demandes = d.Parametres;
        if (demandes.HasFlag(ParametresDemandes.Exercice) && (p.ExerciceId is not { } exerciceId || !await ctx.Exercices.AnyAsync(e => e.Id == exerciceId)))
            return "Choisissez l'exercice.";
        if (demandes.HasFlag(ParametresDemandes.Axe) && d.Code == "SITUATION_AXE" && p.AxeId is null)
            return "Choisissez l'axe.";
        if (demandes.HasFlag(ParametresDemandes.Cycle) && (p.CycleId is not { } cycleId || !await ctx.CyclesProgrammation.AnyAsync(c => c.Id == cycleId)))
            return "Choisissez le cycle de programmation.";

        TypePeriode? typeExige = demandes.HasFlag(ParametresDemandes.Trimestre) ? TypePeriode.Trimestre
                               : demandes.HasFlag(ParametresDemandes.Semestre) ? TypePeriode.Semestre : null;
        if (typeExige is not null && p.PeriodeId is null)
            return typeExige == TypePeriode.Trimestre ? "Choisissez le trimestre." : "Choisissez le semestre.";
        if (p.PeriodeId is { } periodeId)
        {
            var periode = await ctx.Periodes.AsNoTracking().Where(x => x.Id == periodeId).Select(x => new { x.ExerciceId, x.TypePeriode }).FirstOrDefaultAsync();
            if (periode is null || periode.ExerciceId != p.ExerciceId) return "La période n'appartient pas à l'exercice choisi.";
            if (typeExige is { } t && periode.TypePeriode != t) return "La période choisie n'est pas du type attendu.";
        }
        if (demandes.HasFlag(ParametresDemandes.Dates))
        {
            if (p.Du is null || p.Au is null) return "Indiquez la période (du / au).";
            if (p.Au < p.Du) return "La date de fin précède la date de début.";
        }
        return null;
    }

    /// <summary>Filtres appliqués, en clair, pour l'en-tête des exports (§ 4.2).</summary>
    private static async Task<List<(string Libelle, string Valeur)>> FiltresAsync(AppDbContext ctx, ParametresRapport p)
    {
        var filtres = new List<(string, string)>();
        if (p.ExerciceId is { } exercice)
            filtres.Add(("Exercice", (await ctx.Exercices.AsNoTracking().Where(e => e.Id == exercice).Select(e => e.Annee).FirstAsync()).ToString()));
        if (p.PeriodeId is { } periode)
        {
            var x = await ctx.Periodes.AsNoTracking().Where(e => e.Id == periode).Select(e => new { e.TypePeriode, e.Numero, e.DateDebut, e.DateFin }).FirstAsync();
            filtres.Add(("Période", $"{LibellesConsommation.Periode(x.TypePeriode, x.Numero, x.DateDebut)} (situation au {x.DateFin:dd/MM/yyyy})"));
        }
        if (p.AxeId is { } axe)
            filtres.Add(("Axe", await ctx.Axes.AsNoTracking().Where(a => a.Id == axe).Select(a => a.Libelle).FirstAsync()));
        if (p.CycleId is { } cycle)
            filtres.Add(("Cycle", await ctx.CyclesProgrammation.AsNoTracking().Where(c => c.Id == cycle).Select(c => c.Libelle).FirstAsync()));
        if (p.ProgrammeId is { } programme)
            filtres.Add(("Programme", await ctx.Programmes.AsNoTracking().Where(x => x.Id == programme).Select(x => x.Code + " — " + x.Libelle).FirstAsync()));
        if (p.Du is { } du && p.Au is { } au)
            filtres.Add(("Du", $"{du:dd/MM/yyyy} au {au:dd/MM/yyyy}"));
        return filtres;
    }

    /// <summary>Identité de l'organisme courant : raison sociale, coordonnées, logo, en-tête et pied paramétrés, devise.</summary>
    internal static async Task<EnteteInstitutionnelle> EnteteAsync(AppDbContext ctx)
    {
        var organisme = await ctx.Organismes.AsNoTracking().Where(o => o.Id == ctx.OrganismeCourantId)
            .Select(o => new { o.RaisonSociale, o.Sigle, o.Adresse, o.Telephone, o.Email }).FirstOrDefaultAsync();
        var parametres = await ctx.ParametresOrganisme.AsNoTracking()
            .Select(x => new
            {
                x.EnteteRapport, x.PiedPageRapport,
                Logo = x.LogoFichier != null ? x.LogoFichier.Contenu : null,
                Devise = x.DeviseReference != null ? x.DeviseReference.Symbole ?? x.DeviseReference.CodeIso : null
            })
            .FirstOrDefaultAsync();
        var contact = string.Join(" · ", new[] { organisme?.Telephone, organisme?.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return new EnteteInstitutionnelle(organisme?.RaisonSociale ?? "Organisme", organisme?.Sigle, organisme?.Adresse,
            contact.Length == 0 ? null : contact, parametres?.Logo, parametres?.EnteteRapport, parametres?.PiedPageRapport, parametres?.Devise ?? "FCFA");
    }
}

/// <summary>
/// Dépôt temporaire des fichiers produits, pour les ouvrir dans un onglet du navigateur
/// (impression directe, § 3.12.3) : 10 minutes, réservé à l'utilisateur qui l'a produit.
/// </summary>
public sealed class DepotRapports
{
    private static readonly TimeSpan Duree = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _cache;

    public DepotRapports(IMemoryCache cache) => _cache = cache;

    public string Deposer(Ulid utilisateurId, FichierRapport fichier)
    {
        var jeton = Ulid.NewUlid().ToString();
        _cache.Set(Cle(jeton), (utilisateurId, fichier), Duree);
        return jeton;
    }

    public FichierRapport? Obtenir(string jeton, Ulid utilisateurId)
        => _cache.TryGetValue(Cle(jeton), out (Ulid Utilisateur, FichierRapport Fichier) depot) && depot.Utilisateur == utilisateurId && utilisateurId != Ulid.Empty
            ? depot.Fichier : null;

    private static string Cle(string jeton) => $"rapport:{jeton}";
}
