using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Plateforme;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.BudgetProgramme;

public sealed record LigneAnalyse(Ulid Id, TypeAnalyse Type, Ulid? ProgrammeId, string? Programme, Ulid? RubriqueId, string? Rubrique,
                                  string Contenu, int Ordre);

/// <summary>Saisie d'une analyse (création si Id vide).</summary>
public sealed class SaisieAnalyse
{
    public Ulid Id { get; set; }
    public Ulid ExerciceId { get; set; }
    public TypeAnalyse Type { get; set; } = TypeAnalyse.Contrainte;
    public Ulid? ProgrammeId { get; set; }
    public Ulid? RubriqueId { get; set; }
    public string Contenu { get; set; } = string.Empty;
    public int Ordre { get; set; }
}

/// <summary>
/// Analyses qualitatives de l'exercice (cahier fonctionnel § 3.3.8) : contraintes,
/// priorités et actions stratégiques, défis, recommandations, commentaires de rubrique.
/// Saisies par les utilisateurs habilités (annexe C) dans un exercice ouvert.
/// </summary>
public interface IAnalyseService
{
    Task<List<LigneAnalyse>> ListerAsync(Ulid exerciceId, TypeAnalyse? type = null, Ulid? programmeId = null);
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieAnalyse saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid id);
}

public sealed class AnalyseService : IAnalyseService
{
    private const int LongueurMax = 20_000;
    private static readonly TypeDroit[] DroitsSaisie = [TypeDroit.BudgetProgrammeDefinir, TypeDroit.IndicateurSaisirRealisation];

    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesBudgetProgramme _acces;
    private readonly IAuditService _audit;

    public AnalyseService(IDbContextFactory<AppDbContext> factory, AccesBudgetProgramme acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<List<LigneAnalyse>> ListerAsync(Ulid exerciceId, TypeAnalyse? type = null, Ulid? programmeId = null)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        var q = ctx.AnalysesQualitatives.AsNoTracking().Where(a => a.ExerciceId == exerciceId);
        if (type is { } t) q = q.Where(a => a.TypeAnalyse == t);
        if (programmeId is { } p) q = q.Where(a => a.ProgrammeId == p);
        return await q.OrderBy(a => a.TypeAnalyse).ThenBy(a => a.Ordre)
            .Select(a => new LigneAnalyse(a.Id, a.TypeAnalyse, a.ProgrammeId, a.Programme == null ? null : a.Programme.Code,
                a.RubriqueId, a.Rubrique == null ? null : a.Rubrique.Code + " — " + a.Rubrique.Libelle, a.Contenu, a.Ordre))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieAnalyse s)
    {
        var acteurId = await _acces.ActeurAsync(DroitsSaisie);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir les analyses"));

        s.Contenu = (s.Contenu ?? "").Trim();
        if (s.Contenu.Length == 0) return (false, "Le texte de l'analyse est obligatoire.");
        if (s.Contenu.Length > LongueurMax) return (false, $"L'analyse ne doit pas dépasser {LongueurMax} caractères.");
        if (!Enum.IsDefined(s.Type)) return (false, "Type d'analyse inconnu.");
        if (s.Type == TypeAnalyse.CommentaireRubrique && s.RubriqueId is null) return (false, "Choisissez la rubrique commentée.");
        if (s.Type != TypeAnalyse.CommentaireRubrique) s.RubriqueId = null;

        await using var ctx = await _factory.CreateDbContextAsync();
        var (_, erreur) = await AccesBudgetProgramme.ExerciceSaisissableAsync(ctx, s.ExerciceId);
        if (erreur is not null) return (false, erreur);
        if (s.ProgrammeId is { } p && !await ctx.Programmes.AnyAsync(x => x.Id == p)) return (false, "Programme introuvable.");
        if (s.RubriqueId is { } r && !await ctx.Rubriques.AnyAsync(x => x.Id == r)) return (false, "Rubrique introuvable.");

        var refus = await _acces.PerimetreAsync(acteurId, s.ProgrammeId is null ? CiblePerimetre.Organisme : new CiblePerimetre(s.ProgrammeId));
        if (refus is not null) return (false, refus);

        AnalyseQualitative analyse;
        if (s.Id == Ulid.Empty)
        {
            analyse = new AnalyseQualitative { ExerciceId = s.ExerciceId, CreePar = acteurId };
            ctx.AnalysesQualitatives.Add(analyse);
        }
        else
        {
            var existante = await ctx.AnalysesQualitatives.FirstOrDefaultAsync(a => a.Id == s.Id && a.ExerciceId == s.ExerciceId);
            if (existante is null) return (false, "Analyse introuvable.");
            // Changer de programme exige aussi le périmètre de celui qu'elle quitte.
            if (existante.ProgrammeId != s.ProgrammeId
                && await _acces.PerimetreAsync(acteurId, existante.ProgrammeId is null ? CiblePerimetre.Organisme : new CiblePerimetre(existante.ProgrammeId)) is { } refusOrigine)
                return (false, refusOrigine);
            analyse = existante;
            analyse.ModifiePar = acteurId;
        }
        analyse.TypeAnalyse = s.Type; analyse.ProgrammeId = s.ProgrammeId; analyse.RubriqueId = s.RubriqueId;
        analyse.Contenu = s.Contenu; analyse.Ordre = s.Ordre;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, s.Id == Ulid.Empty ? "CREATION" : "MODIFICATION", AccesBudgetProgramme.ModuleAudit,
            nameof(AnalyseQualitative), analyse.Id.ToString(), null, new { Type = s.Type.ToString() });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid id)
    {
        var acteurId = await _acces.ActeurAsync(DroitsSaisie);
        if (acteurId == Ulid.Empty) return (false, AccesBudgetProgramme.MessageDroit("saisir les analyses"));

        await using var ctx = await _factory.CreateDbContextAsync();
        var analyse = await ctx.AnalysesQualitatives.FirstOrDefaultAsync(a => a.Id == id);
        if (analyse is null) return (false, "Analyse introuvable.");
        var (_, erreur) = await AccesBudgetProgramme.ExerciceSaisissableAsync(ctx, analyse.ExerciceId);
        if (erreur is not null) return (false, erreur);
        var refus = await _acces.PerimetreAsync(acteurId, analyse.ProgrammeId is null ? CiblePerimetre.Organisme : new CiblePerimetre(analyse.ProgrammeId));
        if (refus is not null) return (false, refus);

        analyse.EstSupprime = true;
        analyse.DateSuppression = DateTime.UtcNow;
        analyse.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();
        return (true, null);
    }
}
