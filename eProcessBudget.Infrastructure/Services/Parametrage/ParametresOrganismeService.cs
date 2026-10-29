using System.Net.Mail;
using System.Security.Cryptography;
using eProcessBudget.Core.Entities;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Infrastructure.Services.Securite;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

/// <summary>Identité et paramètres généraux de l'organisme, tels qu'édités à l'écran.</summary>
public sealed class SaisieParametresOrganisme
{
    // ── Identité (plat.Organisme) ───────────────────────────────────────────
    public string Code { get; set; } = string.Empty;   // lecture seule
    public string RaisonSociale { get; set; } = string.Empty;
    public string? Sigle { get; set; }
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? TutelleTechnique { get; set; }
    public string? TutelleFinanciere { get; set; }

    // ── Paramètres (cfg.ParametreOrganisme) ─────────────────────────────────
    public Ulid? DeviseReferenceId { get; set; }
    public int NbDecimales { get; set; } = 2;
    public int DureeSessionMinutes { get; set; } = 30;
    public int NbTentativesMax { get; set; } = 5;
    public int TaillePagination { get; set; } = 20;
    public int RetentionJournauxMois { get; set; } = 12;
    public string? EnteteRapport { get; set; }
    public string? PiedPageRapport { get; set; }

    public bool AUnLogo { get; set; }
}

/// <summary>
/// Paramètres de l'organisme courant (cahier fonctionnel § 3.2.2) : identité (en-tête des
/// écrans et des rapports), logo, devise de référence, paramètres techniques (session,
/// tentatives, pagination, rétention), en-tête et pied de page des exports.
///
/// Le code et le statut de l'organisme restent l'affaire de l'Administrateur plateforme.
/// </summary>
public interface IParametresOrganismeService
{
    Task<SaisieParametresOrganisme?> ObtenirAsync();
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieParametresOrganisme saisie);

    /// <summary>Logo PNG ou JPEG, 512 Ko au plus (vérifié sur le contenu, pas sur l'extension).</summary>
    Task<(bool Ok, string? Erreur)> DefinirLogoAsync(Stream contenu, string nomFichier);
    Task<(bool Ok, string? Erreur)> SupprimerLogoAsync();

    /// <summary>Logo de l'organisme de l'utilisateur connecté, ou null.</summary>
    Task<(byte[] Contenu, string ContentType)?> ObtenirLogoAsync();

    /// <summary>Ajoute les données de référence initiales manquantes (§ 3.7), sans rien écraser.</summary>
    Task<(bool Ok, string? Erreur, int NbAjouts)> CompleterDonneesInitialesAsync();
}

public sealed class ParametresOrganismeService : IParametresOrganismeService
{
    public const int TailleLogoMax = 512 * 1024;

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IUserContextService _utilisateurCourant;
    private readonly IAuditService _audit;

    public ParametresOrganismeService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces,
                                      IUserContextService utilisateurCourant, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _utilisateurCourant = utilisateurCourant;
        _audit = audit;
    }

    public async Task<SaisieParametresOrganisme?> ObtenirAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        var organismeId = await _utilisateurCourant.ObtenirOrganismeIdAsync();

        await using var ctx = await _factory.CreateDbContextAsync();
        var o = await ctx.Organismes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == organismeId);
        if (o is null) return null;

        var p = await ctx.ParametresOrganisme.AsNoTracking().FirstOrDefaultAsync() ?? new ParametreOrganisme();
        return new SaisieParametresOrganisme
        {
            Code = o.Code, RaisonSociale = o.RaisonSociale, Sigle = o.Sigle, Adresse = o.Adresse, Telephone = o.Telephone,
            Email = o.Email, TutelleTechnique = o.TutelleTechnique, TutelleFinanciere = o.TutelleFinanciere,
            DeviseReferenceId = p.DeviseReferenceId, NbDecimales = p.NbDecimales, DureeSessionMinutes = p.DureeSessionMinutes,
            NbTentativesMax = p.NbTentativesMax, TaillePagination = p.TaillePagination, RetentionJournauxMois = p.RetentionJournauxMois,
            EnteteRapport = p.EnteteRapport, PiedPageRapport = p.PiedPageRapport, AUnLogo = p.LogoFichierId is not null
        };
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieParametresOrganisme s)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        static string? F(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        s.RaisonSociale = (s.RaisonSociale ?? "").Trim();
        s.Sigle = F(s.Sigle); s.Adresse = F(s.Adresse); s.Telephone = F(s.Telephone); s.Email = F(s.Email);
        s.TutelleTechnique = F(s.TutelleTechnique); s.TutelleFinanciere = F(s.TutelleFinanciere);
        s.EnteteRapport = F(s.EnteteRapport); s.PiedPageRapport = F(s.PiedPageRapport);

        var erreur = Valider(s);
        if (erreur is not null) return (false, erreur);

        var organismeId = await _utilisateurCourant.ObtenirOrganismeIdAsync();
        await using var ctx = await _factory.CreateDbContextAsync();

        var o = await ctx.Organismes.FirstOrDefaultAsync(x => x.Id == organismeId);
        if (o is null) return (false, "Organisme introuvable.");

        if (s.DeviseReferenceId is { } deviseId && !await ctx.Devises.AnyAsync(d => d.Id == deviseId))
            return (false, "La devise de référence est introuvable.");

        var p = await ctx.ParametresOrganisme.FirstOrDefaultAsync();
        if (p is null) ctx.ParametresOrganisme.Add(p = new ParametreOrganisme { CreePar = acteurId });

        var avant = new { o.RaisonSociale, o.Sigle, p.DeviseReferenceId, p.NbDecimales, p.DureeSessionMinutes, p.NbTentativesMax, p.TaillePagination, p.RetentionJournauxMois };

        o.RaisonSociale = s.RaisonSociale; o.Sigle = s.Sigle; o.Adresse = s.Adresse; o.Telephone = s.Telephone; o.Email = s.Email;
        o.TutelleTechnique = s.TutelleTechnique; o.TutelleFinanciere = s.TutelleFinanciere; o.ModifiePar = acteurId;

        p.DeviseReferenceId = s.DeviseReferenceId; p.NbDecimales = s.NbDecimales; p.DureeSessionMinutes = s.DureeSessionMinutes;
        p.NbTentativesMax = s.NbTentativesMax; p.TaillePagination = s.TaillePagination; p.RetentionJournauxMois = s.RetentionJournauxMois;
        p.EnteteRapport = s.EnteteRapport; p.PiedPageRapport = s.PiedPageRapport; p.ModifiePar = acteurId;

        // Une seule devise de référence : l'indicateur EstReference suit le paramètre.
        foreach (var devise in await ctx.Devises.Where(d => d.EstReference || d.Id == s.DeviseReferenceId).ToListAsync())
            devise.EstReference = devise.Id == s.DeviseReferenceId;

        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "MODIFICATION", AccesParametrage.ModuleAudit, nameof(ParametreOrganisme),
            p.Id.ToString(), avant,
            new { o.RaisonSociale, o.Sigle, p.DeviseReferenceId, p.NbDecimales, p.DureeSessionMinutes, p.NbTentativesMax, p.TaillePagination, p.RetentionJournauxMois });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> DefinirLogoAsync(Stream contenu, string nomFichier)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        // Lecture bornée : on ne charge jamais plus que la taille admise.
        using var tampon = new MemoryStream();
        var bloc = new byte[81920];
        int lus;
        while ((lus = await contenu.ReadAsync(bloc)) > 0)
        {
            if (tampon.Length + lus > TailleLogoMax) return (false, "Le logo ne doit pas dépasser 512 Ko.");
            tampon.Write(bloc, 0, lus);
        }
        var octets = tampon.ToArray();

        // Type déterminé par la signature du fichier : PNG ou JPEG uniquement (pas de SVG,
        // qui peut embarquer du script).
        var contentType = octets is [0x89, 0x50, 0x4E, 0x47, ..] ? "image/png"
                        : octets is [0xFF, 0xD8, 0xFF, ..] ? "image/jpeg"
                        : null;
        if (contentType is null) return (false, "Le logo doit être une image PNG ou JPEG.");

        await using var ctx = await _factory.CreateDbContextAsync();
        var p = await ctx.ParametresOrganisme.FirstOrDefaultAsync();
        if (p is null) return (false, "Enregistrez d'abord les paramètres de l'organisme.");

        var fichier = new Fichier
        {
            NomFichier = Path.GetFileName(nomFichier) is { Length: > 0 and <= 255 } n ? n : "logo",
            ContentType = contentType,
            Taille = octets.Length,
            Empreinte = Convert.ToHexString(SHA256.HashData(octets)),
            Contenu = octets,
            CreePar = acteurId
        };
        ctx.Fichiers.Add(fichier);

        await SupprimerFichierAsync(ctx, p.LogoFichierId, acteurId);
        p.LogoFichierId = fichier.Id;
        p.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "LOGO", AccesParametrage.ModuleAudit, nameof(ParametreOrganisme),
            p.Id.ToString(), null, new { fichier.NomFichier, fichier.Taille });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerLogoAsync()
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var p = await ctx.ParametresOrganisme.FirstOrDefaultAsync();
        if (p?.LogoFichierId is null) return (true, null);

        await SupprimerFichierAsync(ctx, p.LogoFichierId, acteurId);
        p.LogoFichierId = null;
        p.ModifiePar = acteurId;
        await ctx.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(byte[] Contenu, string ContentType)?> ObtenirLogoAsync()
    {
        if (await _acces.MembreOrganismeAsync() == Ulid.Empty) return null;

        await using var ctx = await _factory.CreateDbContextAsync();
        var logo = await ctx.ParametresOrganisme.AsNoTracking()
            .Where(p => p.LogoFichier != null)
            .Select(p => new { p.LogoFichier!.Contenu, p.LogoFichier.ContentType })
            .FirstOrDefaultAsync();
        return logo is null ? null : (logo.Contenu, logo.ContentType);
    }

    public async Task<(bool Ok, string? Erreur, int NbAjouts)> CompleterDonneesInitialesAsync()
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion, 0);
        var organismeId = await _utilisateurCourant.ObtenirOrganismeIdAsync();

        await using var ctx = await _factory.CreateDbContextAsync();
        var nb = await ProvisionnementOrganisme.CompleterAsync(ctx, organismeId, acteurId);
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "DONNEES_INITIALES", AccesParametrage.ModuleAudit, nameof(Organisme),
            organismeId.ToString(), commentaire: $"{nb} élément(s) ajouté(s)");
        return (true, null, nb);
    }

    private static async Task SupprimerFichierAsync(AppDbContext ctx, Ulid? fichierId, Ulid acteurId)
    {
        if (fichierId is null) return;
        var ancien = await ctx.Fichiers.FirstOrDefaultAsync(f => f.Id == fichierId);
        if (ancien is null) return;
        ancien.EstSupprime = true;
        ancien.DateSuppression = DateTime.UtcNow;
        ancien.SupprimePar = acteurId;
    }

    private static string? Valider(SaisieParametresOrganisme s)
    {
        if (s.RaisonSociale.Length == 0) return "La raison sociale est obligatoire.";
        if (s.RaisonSociale.Length > 200) return "La raison sociale ne doit pas dépasser 200 caractères.";
        if (s.Sigle?.Length > 30) return "Le sigle ne doit pas dépasser 30 caractères.";
        if (s.Adresse?.Length > 300) return "L'adresse ne doit pas dépasser 300 caractères.";
        if (s.Telephone?.Length > 30) return "Le téléphone ne doit pas dépasser 30 caractères.";
        if (s.Email is not null && (s.Email.Length > 150 || !MailAddress.TryCreate(s.Email, out _))) return "L'adresse e-mail n'est pas valide.";
        if (s.TutelleTechnique?.Length > 200 || s.TutelleFinanciere?.Length > 200) return "Une tutelle ne doit pas dépasser 200 caractères.";
        if (s.NbDecimales is < 0 or > 4) return "Le nombre de décimales est compris entre 0 et 4.";
        if (s.DureeSessionMinutes is < 5 or > 1440) return "La durée de session est comprise entre 5 et 1 440 minutes.";
        if (s.NbTentativesMax is < 1 or > 20) return "Le nombre de tentatives est compris entre 1 et 20.";
        if (s.TaillePagination is < 10 or > 200) return "La pagination est comprise entre 10 et 200 lignes.";
        if (s.RetentionJournauxMois is < 12 or > 120) return "La rétention des journaux est comprise entre 12 et 120 mois.";
        if (s.EnteteRapport?.Length > 500 || s.PiedPageRapport?.Length > 500) return "L'en-tête et le pied de page ne dépassent pas 500 caractères.";
        return null;
    }
}
