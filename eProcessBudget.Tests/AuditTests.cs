using System.Text.Json;
using ClosedXML.Excel;
using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Rapports;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Tests;

/// <summary>Tests clés de l'audit et de la traçabilité (cahier technique § 2.7 et 4.9).</summary>
public class AuditTests
{
    static AuditTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static async Task<(Environnement Env, Ulid Organisme, Utilisateur Auditeur)> PreparerAsync()
    {
        var env = new Environnement();
        var org = await env.CreerOrganismeAsync("ORG");
        var auditeur = await env.CreerUtilisateurAsync("auditeur", org);
        env.Droits.Accorder(auditeur.Id, TypeDroit.AuditConsultation, TypeDroit.AuditExportation);
        env.UtilisateurCourant.Utilisateur = auditeur;
        return (env, org, auditeur);
    }

    private static Dictionary<string, JsonElement> Json(string? texte) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(texte!)!;

    // ═════ Journalisation automatique ═════════════════════════════════════════

    [Fact]
    public async Task Creation_modification_et_suppression_logique_sont_journalisees_avec_les_valeurs()
    {
        var (env, org, auditeur) = await PreparerAsync();
        var structure = new Structure { Code = "DG", Libelle = "Direction générale", CreePar = auditeur.Id };
        await using (var ctx = env.Contexte(org))
        {
            ctx.AdresseIp = "10.0.0.7";
            ctx.CorrelationId = "circuit-1";
            ctx.Structures.Add(structure);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = env.Contexte(org))
        {
            var creation = await ctx.AuditLogs.SingleAsync(a => a.Module == "Données" && a.Entite == nameof(Structure));
            Assert.Equal("CREATION", creation.Action);
            Assert.Equal(nameof(Structure), creation.Entite);
            Assert.Equal(structure.Id.ToString(), creation.EntiteId);
            Assert.Equal(org, creation.OrganismeId);
            Assert.Equal(auditeur.Id, creation.UtilisateurId);
            Assert.Equal("10.0.0.7", creation.AdresseIP);
            Assert.Equal("circuit-1", creation.CorrelationId);
            var apres = Json(creation.NouvelleValeur);
            Assert.Equal("DG", apres["Code"].GetString());
            Assert.False(apres.ContainsKey("DateCreation"));                  // colonnes techniques exclues
            Assert.False(apres.ContainsKey("RowVersion"));
            Assert.Null(creation.AncienneValeur);

            // Modification : seuls les champs modifiés, avant et après.
            var s = await ctx.Structures.FirstAsync();
            s.Libelle = "Direction générale (DG)";
            s.ModifiePar = auditeur.Id;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = env.Contexte(org))
        {
            var modification = await ctx.AuditLogs.SingleAsync(a => a.Action == "MODIFICATION" && a.Entite == nameof(Structure));
            Assert.Equal(["Libelle"], Json(modification.NouvelleValeur).Keys);
            Assert.Equal("Direction générale", Json(modification.AncienneValeur)["Libelle"].GetString());

            // Suppression logique : les valeurs avant ; la date de suppression est posée.
            var s = await ctx.Structures.FirstAsync();
            s.EstSupprime = true;
            s.SupprimePar = auditeur.Id;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = env.Contexte(org))
        {
            var suppression = await ctx.AuditLogs.SingleAsync(a => a.Action == "SUPPRESSION_LOGIQUE" && a.Entite == nameof(Structure));
            Assert.Equal("Direction générale (DG)", Json(suppression.AncienneValeur)["Libelle"].GetString());
            Assert.NotNull((await ctx.Structures.IgnoreQueryFilters().FirstAsync()).DateSuppression);
        }
    }

    [Fact]
    public async Task Les_champs_sensibles_et_le_suivi_de_connexion_ne_sont_pas_journalises()
    {
        var (env, org, auditeur) = await PreparerAsync();
        await using (var ctx = env.Contexte(org))
        {
            // La création du compte est journalisée ; on regarde ce qui s'ajoute ensuite.
            IQueryable<AuditLog> Modifications() => ctx.AuditLogs.Where(a => a.Entite == nameof(Utilisateur) && a.Action == "MODIFICATION");
            var u = await ctx.Utilisateurs.FirstAsync(x => x.Id == auditeur.Id);
            u.PasswordHash = "nouveau-hachage";
            u.DerniereConnexion = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            Assert.Equal(0, await Modifications().CountAsync());   // rien d'autre n'a changé

            u.NomPrenoms = "Auditeur principal";
            u.PasswordHash = "encore-un-autre";
            await ctx.SaveChangesAsync();
            var ligne = await Modifications().SingleAsync();
            Assert.Equal(["NomPrenoms"], Json(ligne.NouvelleValeur).Keys);
            Assert.DoesNotContain("hachage", ligne.AncienneValeur + ligne.NouvelleValeur);
        }
    }

    [Fact]
    public async Task Les_journaux_sont_en_ajout_seul()
    {
        var (env, org, _) = await PreparerAsync();
        await using var ctx = env.Contexte(org);
        ctx.AuditLogs.Add(new AuditLog { Id = Ulid.NewUlid(), OrganismeId = org, Action = "TEST", Module = "Test", Entite = "Test" });
        ctx.JournalConnexions.Add(new JournalConnexion { Id = Ulid.NewUlid(), OrganismeId = org, Login = "x", AdresseIp = "1.1.1.1", Statut = StatutConnexion.Succes });
        await ctx.SaveChangesAsync();

        var ligne = await ctx.AuditLogs.FirstAsync(a => a.Action == "TEST");
        ligne.Commentaire = "retouche";
        Assert.Throws<InvalidOperationException>(() => ctx.SaveChanges());
        ctx.Entry(ligne).State = EntityState.Unchanged;

        ctx.JournalConnexions.Remove(await ctx.JournalConnexions.FirstAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
    }

    [Fact]
    public async Task Un_enregistrement_retente_ne_duplique_pas_le_journal_automatique()
    {
        // Premier enregistrement en échec (collision de numéro, concurrence...), puis nouvel essai
        // sur le même contexte, comme le font les services qui numérotent leurs pièces.
        var options = new DbContextOptionsBuilder<Infrastructure.Data.AppDbContext>()
            .UseInMemoryDatabase($"tests-{Guid.NewGuid()}").AddInterceptors(new EchecAuPremierEnregistrement()).Options;
        await using var ctx = new Infrastructure.Data.AppDbContext(options) { OrganismeCourantId = Ulid.NewUlid() };
        var structure = new Structure { Code = "S1", Libelle = "Service" };
        ctx.Structures.Add(structure);

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync());
        await ctx.SaveChangesAsync();

        Assert.Equal(1, await ctx.AuditLogs.CountAsync(a => a.EntiteId == structure.Id.ToString()));
    }

    private sealed class EchecAuPremierEnregistrement : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private bool _echoue;

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (_echoue) return ValueTask.FromResult(result);
            _echoue = true;
            throw new DbUpdateException("Échec simulé du premier enregistrement.");
        }
    }

    [Fact]
    public async Task Purge_au_dela_de_la_retention_minimum_douze_mois()
    {
        var (env, org, _) = await PreparerAsync();
        var maintenant = new DateTime(2027, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        await using (var ctx = env.Contexte(org))
        {
            foreach (var mois in new[] { 30, 14, 11, 1 })
                ctx.AuditLogs.Add(new AuditLog { Id = Ulid.NewUlid(), OrganismeId = org, Action = $"A{mois}", Module = "Test", Entite = "Test", DateAction = maintenant.AddMonths(-mois) });
            ctx.JournalConnexions.Add(new JournalConnexion { Id = Ulid.NewUlid(), OrganismeId = org, Login = "x", AdresseIp = "1.1.1.1", DateHeure = maintenant.AddMonths(-20) });
            await ctx.SaveChangesAsync();

            // Une rétention de 6 mois est portée au minimum de 12.
            var r = await PurgeJournaux.PurgerAsync(ctx, org, 6, maintenant);
            Assert.Equal(2, r.LignesAudit);
            Assert.Equal(1, r.LignesConnexion);
        }
        await using (var ctx = env.Contexte(org))
        {
            Assert.Equal(["A1", "A11", "PURGE"], (await ctx.AuditLogs.Where(a => a.Module != "Données").Select(a => a.Action).ToListAsync()).Order());
            Assert.Empty(await ctx.JournalConnexions.ToListAsync());

            // Rétention de l'organisme (24 mois) : la ligne de 14 mois aurait été gardée.
            ctx.AuditLogs.Add(new AuditLog { Id = Ulid.NewUlid(), OrganismeId = org, Action = "A14", Module = "Test", Entite = "Test", DateAction = maintenant.AddMonths(-14) });
            await ctx.SaveChangesAsync();
            Assert.Equal(0, (await PurgeJournaux.PurgerAsync(ctx, org, 24, maintenant)).Total);
        }
    }

    [Fact]
    public void Le_contexte_de_connexion_garde_la_premiere_adresse()
    {
        var c = new ContexteConnexion();
        Assert.NotEmpty(c.CorrelationId);
        c.Definir("192.168.1.10");
        c.Definir("10.0.0.1", "requete-42");
        Assert.Equal("192.168.1.10", c.AdresseIp);
        Assert.Equal("requete-42", c.CorrelationId);
    }

    // ═════ Consultation et export ═════════════════════════════════════════════

    private static JournalAuditService Service(Environnement env, Ulid org, AuditFactice audit) => new(env.Fabrique(org), env.UtilisateurCourant, env.Droits, audit);

    private static async Task SemerAsync(Environnement env, Ulid org, Utilisateur auteur)
    {
        await using var ctx = env.Contexte(org);
        ctx.Structures.Add(new Structure { Code = "DG", Libelle = "Direction", CreePar = auteur.Id });
        ctx.AuditLogs.Add(new AuditLog
        {
            Id = Ulid.NewUlid(), OrganismeId = org, UtilisateurId = auteur.Id, UtilisateurMatricule = auteur.Matricule, Action = "VALIDATION",
            Module = "Execution", Entite = "Engagement", EntiteId = "ENG-1", AdresseIP = "10.1.1.1", DateAction = DateTime.UtcNow.AddDays(-2)
        });
        ctx.AuditLogs.Add(new AuditLog { Id = Ulid.NewUlid(), OrganismeId = org, Action = "CONNEXION", Module = "Securite", Entite = "Utilisateur", EstSucces = false, DateAction = DateTime.UtcNow.AddDays(-40) });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Consultation_filtres_multicriteres_cloisonnement_et_detail()
    {
        var (env, org, auditeur) = await PreparerAsync();
        await SemerAsync(env, org, auditeur);
        var autre = await env.CreerOrganismeAsync("AUTRE");
        await using (var ctx = env.Contexte(autre))
        {
            ctx.AuditLogs.Add(new AuditLog { Id = Ulid.NewUlid(), OrganismeId = autre, Action = "VALIDATION", Module = "Execution", Entite = "Engagement" });
            await ctx.SaveChangesAsync();
        }
        var service = Service(env, org, new AuditFactice());

        // Événements métier : les deux semés, pas celui de l'autre organisme.
        Assert.Equal(2, (await service.RechercherAsync(new FiltreAudit { Automatique = false })).Total);
        var automatiques = (await service.RechercherAsync(new FiltreAudit { Automatique = true })).Total;
        Assert.True(automatiques >= 1);
        Assert.Equal(automatiques + 2, (await service.RechercherAsync(new FiltreAudit())).Total);
        Assert.Equal("ENG-1", Assert.Single((await service.RechercherAsync(new FiltreAudit { Module = "Execution", Action = "VALIDATION" })).Items).EntiteId);
        Assert.Equal(1, (await service.RechercherAsync(new FiltreAudit { EstSucces = false })).Total);
        Assert.Equal(1, (await service.RechercherAsync(new FiltreAudit { Recherche = "10.1.1" })).Total);
        // L'auteur : la validation semée et la création de la structure (CreePar).
        Assert.Equal(2, (await service.RechercherAsync(new FiltreAudit { UtilisateurId = auditeur.Id })).Items.Count(l => l.Entite is nameof(Structure) or "Engagement"));
        Assert.Equal(0, (await service.RechercherAsync(new FiltreAudit { Automatique = false, Au = DateOnly.FromDateTime(DateTime.Today.AddDays(-50)) })).Total);
        Assert.Equal(1, (await service.RechercherAsync(new FiltreAudit { Automatique = false, Du = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)) })).Total);

        var (modules, actions, entites) = await service.ListerValeursAsync();
        Assert.Contains("Données", modules);
        Assert.Contains("CREATION", actions);
        Assert.Contains(nameof(Structure), entites);

        var creation = Assert.Single((await service.RechercherAsync(new FiltreAudit { Entite = nameof(Structure) })).Items);
        var detail = (await service.ObtenirAsync(creation.Id))!;
        Assert.Equal("DG", detail.Champs.Single(c => c.Champ == "Code").Apres);
        Assert.True(detail.Champs.All(c => c.Avant is null && c.EstModifie));

        // Sans AuditConsultation : rien.
        var lecteur = await env.CreerUtilisateurAsync("lecteur", org);
        env.UtilisateurCourant.Utilisateur = lecteur;
        Assert.Equal(0, (await service.RechercherAsync(new FiltreAudit())).Total);
        Assert.Null(await service.ObtenirAsync(creation.Id));
    }

    [Fact]
    public void Comparaison_avant_apres_champ_par_champ()
    {
        var champs = JournalAuditService.Comparer("""{"Libelle":"A","Montant":10,"Code":"X"}""", """{"Libelle":"B","Montant":10,"Statut":"Valide"}""");
        Assert.Equal(["Libelle", "Montant", "Code", "Statut"], champs.Select(c => c.Champ));
        Assert.True(champs.Single(c => c.Champ == "Libelle").EstModifie);
        Assert.False(champs.Single(c => c.Champ == "Montant").EstModifie);
        Assert.Equal("10", champs.Single(c => c.Champ == "Montant").Apres);
        Assert.Null(champs.Single(c => c.Champ == "Statut").Avant);

        Assert.Equal("Valeur", Assert.Single(JournalAuditService.Comparer("texte libre", null)).Champ);
        Assert.Empty(JournalAuditService.Comparer(null, null));
    }

    [Fact]
    public async Task Export_du_journal_avec_droit_filtres_et_trace()
    {
        var (env, org, auditeur) = await PreparerAsync();
        await SemerAsync(env, org, auditeur);
        var audit = new AuditFactice();
        var service = Service(env, org, audit);

        var (ok, erreur, fichier) = await service.ExporterAsync(new FiltreAudit { Module = "Execution" }, FormatRapport.Excel);
        Assert.True(ok, erreur);
        using (var classeur = new XLWorkbook(new MemoryStream(fichier!.Contenu)))
        {
            var textes = classeur.Worksheet("Opérations journalisées").CellsUsed().Select(c => c.GetString()).ToList();
            Assert.Contains("Module : Execution", textes);
            Assert.Contains("ENG-1", textes);
            Assert.DoesNotContain("CONNEXION", textes);
        }
        var pdf = await service.ExporterAsync(new FiltreAudit(), FormatRapport.Pdf);
        Assert.True(pdf.Ok, pdf.Erreur);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Fichier!.Contenu, 0, 4));
        Assert.Equal(2, audit.Actions.Count(a => a == "EXPORT"));

        // Consulter sans exporter : l'export est refusé.
        var lecteur = await env.CreerUtilisateurAsync("lecteur", org);
        env.Droits.Accorder(lecteur.Id, TypeDroit.AuditConsultation);
        env.UtilisateurCourant.Utilisateur = lecteur;
        Assert.Contains("exporter", (await service.ExporterAsync(new FiltreAudit(), FormatRapport.Excel)).Erreur);
    }
}
