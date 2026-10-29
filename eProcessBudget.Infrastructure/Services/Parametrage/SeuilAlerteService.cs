using System.Text.RegularExpressions;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Infrastructure.Services.Parametrage;

public sealed record LigneSeuil(Ulid Id, PorteeSeuil Portee, Ulid? AxeId, Ulid? LigneBudgetaireId, string? Cible,
                                decimal Pourcentage, string? Couleur);

/// <summary>Saisie d'un seuil (création si Id vide).</summary>
public sealed class SaisieSeuil
{
    public Ulid Id { get; set; }
    public PorteeSeuil Portee { get; set; } = PorteeSeuil.Globale;
    public Ulid? AxeId { get; set; }
    public Ulid? LigneBudgetaireId { get; set; }
    public decimal Pourcentage { get; set; } = 80;
    public string? Couleur { get; set; } = "#F59E0B";
}

/// <summary>
/// Seuils d'alerte de consommation (80 / 90 / 100 % par défaut), applicables globalement,
/// par axe ou par ligne budgétaire (cahier fonctionnel § 3.2.2). Les alertes elles-mêmes
/// sont calculées par le traitement planifié du module Consommation.
/// </summary>
public interface ISeuilAlerteService
{
    Task<List<LigneSeuil>> ListerAsync();
    Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieSeuil saisie);
    Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid id);
}

public sealed partial class SeuilAlerteService : ISeuilAlerteService
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly AccesParametrage _acces;
    private readonly IAuditService _audit;

    public SeuilAlerteService(IDbContextFactory<AppDbContext> factory, AccesParametrage acces, IAuditService audit)
    {
        _factory = factory;
        _acces = acces;
        _audit = audit;
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex CouleurHexa();

    public async Task<List<LigneSeuil>> ListerAsync()
    {
        if (await _acces.LecteurAsync() == Ulid.Empty) return [];

        await using var ctx = await _factory.CreateDbContextAsync();
        return await ctx.SeuilsAlerte.AsNoTracking()
            .OrderBy(s => s.Portee).ThenBy(s => s.Pourcentage)
            .Select(s => new LigneSeuil(s.Id, s.Portee, s.AxeId, s.LigneBudgetaireId,
                s.Axe != null ? s.Axe.Code + " — " + s.Axe.Libelle
                : s.LigneBudgetaire != null ? s.LigneBudgetaire.Code + " — " + s.LigneBudgetaire.Libelle : null,
                s.Pourcentage, s.Couleur))
            .ToListAsync();
    }

    public async Task<(bool Ok, string? Erreur)> EnregistrerAsync(SaisieSeuil s)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        // Une cible et une seule, selon la portée.
        if (s.Portee != PorteeSeuil.Axe) s.AxeId = null;
        if (s.Portee != PorteeSeuil.LigneBudgetaire) s.LigneBudgetaireId = null;
        s.Couleur = string.IsNullOrWhiteSpace(s.Couleur) ? null : s.Couleur.Trim();

        if (!Enum.IsDefined(s.Portee)) return (false, "Choisissez la portée du seuil.");
        if (s.Portee == PorteeSeuil.Axe && s.AxeId is null) return (false, "Choisissez l'axe.");
        if (s.Portee == PorteeSeuil.LigneBudgetaire && s.LigneBudgetaireId is null) return (false, "Choisissez la ligne budgétaire.");
        if (s.Pourcentage is <= 0 or > 1000) return (false, "Le seuil est un pourcentage compris entre 0 (exclu) et 1 000.");
        if (s.Couleur is not null && !CouleurHexa().IsMatch(s.Couleur)) return (false, "La couleur s'écrit #RRGGBB.");

        await using var ctx = await _factory.CreateDbContextAsync();

        if (s.AxeId is { } axeId && !await ctx.Axes.AnyAsync(a => a.Id == axeId)) return (false, "Axe introuvable.");
        if (s.LigneBudgetaireId is { } ligneId && !await ctx.LignesBudgetaires.AnyAsync(l => l.Id == ligneId)) return (false, "Ligne budgétaire introuvable.");

        if (await ctx.SeuilsAlerte.AnyAsync(x => x.Id != s.Id && x.Portee == s.Portee && x.AxeId == s.AxeId
                                              && x.LigneBudgetaireId == s.LigneBudgetaireId && x.Pourcentage == s.Pourcentage))
            return (false, "Ce seuil existe déjà pour cette portée.");

        SeuilAlerte seuil;
        if (s.Id == Ulid.Empty)
        {
            seuil = new SeuilAlerte { CreePar = acteurId };
            ctx.SeuilsAlerte.Add(seuil);
        }
        else
        {
            var existant = await ctx.SeuilsAlerte.FirstOrDefaultAsync(x => x.Id == s.Id);
            if (existant is null) return (false, "Seuil introuvable.");
            seuil = existant;
            seuil.ModifiePar = acteurId;
        }

        seuil.Portee = s.Portee; seuil.AxeId = s.AxeId; seuil.LigneBudgetaireId = s.LigneBudgetaireId;
        seuil.Pourcentage = s.Pourcentage; seuil.Couleur = s.Couleur;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, s.Id == Ulid.Empty ? "CREATION" : "MODIFICATION", AccesParametrage.ModuleAudit,
            nameof(SeuilAlerte), seuil.Id.ToString(), null, new { Portee = seuil.Portee.ToString(), seuil.Pourcentage });
        return (true, null);
    }

    public async Task<(bool Ok, string? Erreur)> SupprimerAsync(Ulid id)
    {
        var acteurId = await _acces.GestionnaireAsync();
        if (acteurId == Ulid.Empty) return (false, AccesParametrage.MessageGestion);

        await using var ctx = await _factory.CreateDbContextAsync();
        var seuil = await ctx.SeuilsAlerte.FirstOrDefaultAsync(x => x.Id == id);
        if (seuil is null) return (false, "Seuil introuvable.");

        seuil.EstSupprime = true;
        seuil.DateSuppression = DateTime.UtcNow;
        seuil.SupprimePar = acteurId;
        await ctx.SaveChangesAsync();

        await _audit.LoggerAsync(acteurId, null, "SUPPRESSION", AccesParametrage.ModuleAudit, nameof(SeuilAlerte),
            id.ToString(), new { Portee = seuil.Portee.ToString(), seuil.Pourcentage }, null);
        return (true, null);
    }
}
