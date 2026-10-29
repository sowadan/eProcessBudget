using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Execution;

/// <summary>Ce que la clôture d'un exercice va produire, et ce qui l'empêche encore.</summary>
public sealed record ApercuCloture(
    int Annee, StatutExercice Statut, int? AnneeSuivante, StatutExercice? StatutSuivant,
    int NbDotations, MontantsDotation Totaux, int NbEncours, decimal TotalEncours, decimal TotalReliquats,
    int NbDotationsACreer, List<string> Blocages)
{
    public bool PeutCloturer => Blocages.Count == 0;
}

/// <summary>Bilan d'une clôture effectuée.</summary>
public sealed record ResultatCloture(int NbReliquats, decimal TotalReliquats, int NbEncours, decimal TotalEncours, int NbDotationsCreees, int NbLignesHistorique);

/// <summary>
/// Clôture d'un exercice (cahier fonctionnel § 4.2, technique § 2.9 et 4.4), lancée par
/// le droit ExerciceCloturer :
///   - chaque engagement validé non soldé devient un encours reporté sur l'exercice suivant
///     (engagement EstReport = 1, et report « encours » du même montant sur la dotation) ;
///   - les crédits ni payés ni en encours sont reportés en « reliquat de crédit » ;
///   - les dotations de destination sont créées au besoin (même imputation, prévision 0) ;
///   - l'exécution (dépenses et recettes) est versée à l'historique (hist.ExecutionHistorique) ;
///   - l'exercice et ses périodes sont clôturés.
/// Tout est enregistré en une seule opération : la clôture réussit entièrement ou pas du tout.
/// </summary>
public interface IClotureService
{
    Task<ApercuCloture?> ApercuAsync(Ulid exerciceId);
    Task<(bool Ok, string? Erreur, ResultatCloture? Resultat)> CloturerAsync(Ulid exerciceId);
}

public sealed class ClotureService : IClotureService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesExecution _acces;
    private readonly IAuditService _audit;

    public ClotureService(IDbContextFactory<AppDbContext> factory, AccesExecution acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    public async Task<ApercuCloture?> ApercuAsync(Ulid exerciceId)
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return null;
        await using var ctx = await _factory.CreateDbContextAsync();
        var plan = await PlanifierAsync(ctx, exerciceId);
        return plan?.Apercu;
    }

    public async Task<(bool Ok, string? Erreur, ResultatCloture? Resultat)> CloturerAsync(Ulid exerciceId)
    {
        var acteurId = await _acces.ActeurAsync(TypeDroit.ExerciceCloturer);
        if (acteurId == Ulid.Empty) return (false, AccesExecution.MessageDroit("clôturer un exercice"), null);

        await using var ctx = await _factory.CreateDbContextAsync();
        var plan = await PlanifierAsync(ctx, exerciceId);
        if (plan is null) return (false, "Exercice introuvable.", null);
        if (!plan.Apercu.PeutCloturer) return (false, string.Join(" ", plan.Apercu.Blocages), null);

        var exercice = await ctx.Exercices.Include(e => e.Periodes).FirstAsync(e => e.Id == exerciceId);
        var suivant = plan.Suivant!;
        var maintenant = DateTime.UtcNow;
        var statutCible = suivant.Statut == StatutExercice.Ouvert ? StatutSaisie.Valide : StatutSaisie.Brouillon;

        // Dotations de destination : même imputation sur l'exercice suivant.
        var existantes = await ctx.Dotations.Where(d => d.ExerciceId == suivant.Id).ToListAsync();
        var cibles = new Dictionary<Ulid, Dotation>();
        var creees = 0;
        Dotation Cible(DotationCalculee d)
        {
            if (cibles.TryGetValue(d.Id, out var c)) return c;
            c = existantes.FirstOrDefault(x => x.LigneBudgetaireId == d.LigneBudgetaireId && x.ProjetId == d.ProjetId
                                            && x.SiteId == d.SiteId && x.StructureId == d.StructureId && x.TiersId == d.TiersId);
            if (c is null)
            {
                c = new Dotation
                {
                    ExerciceId = suivant.Id, LigneBudgetaireId = d.LigneBudgetaireId, ProjetId = d.ProjetId, SiteId = d.SiteId,
                    StructureId = d.StructureId, TiersId = d.TiersId, MontantInitial = 0, Statut = statutCible, CreePar = acteurId
                };
                ctx.Dotations.Add(c);
                existantes.Add(c);
                creees++;
            }
            return cibles[d.Id] = c;
        }

        // Encours : un engagement reporté par engagement non soldé.
        foreach (var en in plan.Encours)
        {
            var dotation = plan.Dotations.First(d => d.Id == en.DotationId);
            var cible = Cible(dotation);
            var reporte = new Engagement
            {
                ExerciceId = suivant.Id, Dotation = cible, ContratId = en.ContratId, TiersId = en.TiersId, TypeEngagement = en.Type,
                Numero = NumeroReport(en.Numero, suivant.Annee), DateEngagement = suivant.DateDebut, Objet = en.Objet,
                Montant = en.Encours, EstReport = true, EngagementOrigineId = en.Id, Statut = StatutSaisie.Valide,
                ValideParId = acteurId, DateValidation = maintenant, CreePar = acteurId
            };
            ctx.Engagements.Add(reporte);
            ctx.Reports.Add(new Report
            {
                ExerciceSourceId = exerciceId, ExerciceCibleId = suivant.Id, DotationSourceId = en.DotationId, DotationCible = cible,
                EngagementSourceId = en.Id, TypeReport = TypeReport.EncoursEngagement, Origine = OrigineReport.ClotureAutomatique,
                Montant = en.Encours, Commentaire = $"Encours de l'engagement {en.Numero}", CreePar = acteurId
            });
        }

        // Reliquats de crédit.
        var reliquats = plan.Dotations.Where(d => d.Montants.ReliquatCredit > 0).ToList();
        foreach (var d in reliquats)
            ctx.Reports.Add(new Report
            {
                ExerciceSourceId = exerciceId, ExerciceCibleId = suivant.Id, DotationSourceId = d.Id, DotationCible = Cible(d),
                TypeReport = TypeReport.ReliquatCredit, Origine = OrigineReport.ClotureAutomatique,
                Montant = d.Montants.ReliquatCredit, CreePar = acteurId
            });

        // Historique d'exécution (base des états pluriannuels).
        var natures = await ctx.LignesBudgetaires.AsNoTracking()
            .Where(l => plan.Dotations.Select(d => d.LigneBudgetaireId).Contains(l.Id))
            .Select(l => new { l.Id, l.NatureDepenseId, l.SousRubrique.RubriqueId }).ToDictionaryAsync(l => l.Id);
        var historique = plan.Dotations
            .GroupBy(d => (d.AxeId, natures[d.LigneBudgetaireId].RubriqueId, natures[d.LigneBudgetaireId].NatureDepenseId, d.SiteId))
            .Select(g =>
            {
                var m = MontantsDotation.Somme(g.Select(d => d.Montants));
                return new ExecutionHistorique
                {
                    Annee = exercice.Annee, AxeId = g.Key.AxeId, RubriqueId = g.Key.RubriqueId, NatureDepenseId = g.Key.NatureDepenseId,
                    SiteId = g.Key.SiteId, Prevision = m.PrevisionAjustee, MontantAutorise = m.AETotal, MontantRealise = m.PaiementsTotal,
                    RestantAPayer = m.ResteAPayerYCReports, Source = SourceHistorique.Calcul, CreePar = acteurId
                };
            })
            .ToList();
        // Recettes : prévision retenue et encaissé, par axe, rubrique, nature et site.
        var recettes = await Recettes.PrevisionRecetteService.Projeter(ctx.PrevisionsRecettes.AsNoTracking().Where(p => p.ExerciceId == exerciceId)).ToListAsync();
        historique.AddRange(recettes
            .GroupBy(p => (p.AxeId, p.RubriqueId, p.NatureRecetteId, p.SiteId))
            .Select(g =>
            {
                var m = MontantsRecette.Somme(g.Select(p => p.Montants));
                return new ExecutionHistorique
                {
                    Annee = exercice.Annee, AxeId = g.Key.AxeId, RubriqueId = g.Key.RubriqueId, NatureRecetteId = g.Key.NatureRecetteId,
                    SiteId = g.Key.SiteId, Prevision = m.PrevisionRetenue, MontantRealise = m.Encaisse, Source = SourceHistorique.Calcul, CreePar = acteurId
                };
            }));
        ctx.ExecutionsHistoriques.AddRange(historique);

        exercice.Statut = StatutExercice.Cloture;
        exercice.DateCloture = maintenant;
        exercice.ClotureParId = acteurId;
        exercice.ModifiePar = acteurId;
        foreach (var p in exercice.Periodes.Where(p => !p.EstCloturee)) p.EstCloturee = true;

        try
        {
            await ctx.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Clôture lancée en même temps par un autre utilisateur : rien n'a été enregistré ici.
            return (false, "L'exercice vient d'être modifié par un autre utilisateur (clôture simultanée ?). Rechargez la page.", null);
        }

        var resultat = new ResultatCloture(reliquats.Count, reliquats.Sum(d => d.Montants.ReliquatCredit),
            plan.Encours.Count, plan.Encours.Sum(e => e.Encours), creees, historique.Count);
        await _audit.LoggerAsync(acteurId, null, "CLOTURE", AccesExecution.ModuleAudit, nameof(Exercice), exercice.Id.ToString(),
            null, new { exercice.Annee, Suivant = suivant.Annee, resultat.NbReliquats, resultat.TotalReliquats, resultat.NbEncours,
                        resultat.TotalEncours, resultat.NbDotationsCreees, resultat.NbLignesHistorique });
        return (true, null, resultat);
    }

    /// <summary>Numéro d'un encours reporté : R2027-ENG-2026-00001 (hors de la série de l'exercice suivant).</summary>
    internal static string NumeroReport(string numero, int anneeCible)
    {
        // Un encours déjà reporté garde son numéro d'origine : R2028-ENG-2026-00001, pas R2028-R2027-...
        var origine = numero.StartsWith('R') && numero.Length > 6 && numero[5] == '-' ? numero[6..] : numero;
        var resultat = $"R{anneeCible}-{origine}";
        return resultat.Length <= 30 ? resultat : resultat[..30];
    }

    private sealed record EncoursEngagement(Ulid Id, Ulid DotationId, string Numero, string Objet, TypeEngagement Type, Ulid? ContratId, Ulid? TiersId, decimal Encours);

    private sealed record Plan(ApercuCloture Apercu, Exercice? Suivant, List<DotationCalculee> Dotations, List<EncoursEngagement> Encours);

    /// <summary>Calcule reliquats et encours et recense les blocages, sans rien écrire.</summary>
    private static async Task<Plan?> PlanifierAsync(AppDbContext ctx, Ulid exerciceId)
    {
        var exercice = await ctx.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Id == exerciceId);
        if (exercice is null) return null;
        var suivant = await ctx.Exercices.AsNoTracking().FirstOrDefaultAsync(e => e.Annee == exercice.Annee + 1);

        var blocages = new List<string>();
        if (exercice.Statut != StatutExercice.Ouvert) blocages.Add("Seul un exercice ouvert se clôture.");
        if (suivant is null) blocages.Add($"Créez d'abord l'exercice {exercice.Annee + 1}, qui recevra les reports.");
        else if (suivant.Statut == StatutExercice.Cloture) blocages.Add($"L'exercice {suivant.Annee} est déjà clôturé.");

        var ajustementsEnAttente = await ctx.Ajustements.CountAsync(a => a.Dotation.ExerciceId == exerciceId && a.Statut == StatutSaisie.Soumis);
        if (ajustementsEnAttente > 0) blocages.Add($"{ajustementsEnAttente} ajustement(s) attendent une validation ou un rejet.");
        var engagementsEnCours = await ctx.Engagements.CountAsync(e => e.ExerciceId == exerciceId
            && (e.Statut == StatutSaisie.Brouillon || e.Statut == StatutSaisie.Soumis));
        if (engagementsEnCours > 0) blocages.Add($"{engagementsEnCours} engagement(s) en brouillon ou soumis : validez-les, rejetez-les ou supprimez-les.");
        var depassements = await ctx.ValidationsDepassement.CountAsync(v => v.Engagement.ExerciceId == exerciceId && v.Statut == StatutValidation.EnAttente);
        if (depassements > 0) blocages.Add($"{depassements} dépassement(s) attendent la décision du Responsable Budget.");

        var dotations = await SituationService.Calculer(ctx, ctx.Dotations.AsNoTracking().Where(d => d.ExerciceId == exerciceId)).ToListAsync();

        var encours = (await ctx.Engagements.AsNoTracking()
                .Where(e => e.ExerciceId == exerciceId && e.Statut == StatutSaisie.Valide)
                .Select(e => new
                {
                    e.Id, e.DotationId, e.Numero, e.Objet, e.TypeEngagement, e.ContratId, e.TiersId, e.Montant,
                    Paye = e.Paiements.Where(p => p.Statut == StatutSaisie.Valide).Sum(p => (decimal?)p.Montant) ?? 0,
                    Rejets = e.RejetsPenalites.Sum(r => (decimal?)r.Montant) ?? 0
                })
                .ToListAsync())
            .Select(e => new EncoursEngagement(e.Id, e.DotationId, e.Numero, e.Objet, e.TypeEngagement, e.ContratId, e.TiersId,
                CalculsExecution.ResteAPayer(e.Montant, e.Paye, e.Rejets)))
            .Where(e => e.Encours > 0)
            .ToList();

        var aCreer = 0;
        if (suivant is not null)
        {
            var imputations = (await ctx.Dotations.AsNoTracking().Where(d => d.ExerciceId == suivant.Id)
                    .Select(d => new { d.LigneBudgetaireId, d.ProjetId, d.SiteId, d.StructureId, d.TiersId }).ToListAsync())
                .Select(d => (d.LigneBudgetaireId, d.ProjetId, d.SiteId, d.StructureId, d.TiersId)).ToHashSet();
            var sources = encours.Select(e => e.DotationId).Concat(dotations.Where(d => d.Montants.ReliquatCredit > 0).Select(d => d.Id)).ToHashSet();
            aCreer = dotations.Where(d => sources.Contains(d.Id))
                .Count(d => !imputations.Contains((d.LigneBudgetaireId, d.ProjetId, d.SiteId, d.StructureId, d.TiersId)));
        }

        var apercu = new ApercuCloture(exercice.Annee, exercice.Statut, suivant?.Annee, suivant?.Statut, dotations.Count,
            MontantsDotation.Somme(dotations.Select(d => d.Montants)), encours.Count, encours.Sum(e => e.Encours),
            dotations.Sum(d => d.Montants.ReliquatCredit), aCreer, blocages);
        return new Plan(apercu, suivant, dotations, encours);
    }
}
