using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Options;
using eProcessBudget.Infrastructure.Services.Securite;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Tests;

/// <summary>Connexion, blocage après N échecs, journal et validité des sessions (§ 2.6, test clé du § 4.1).</summary>
public class AuthentificationTests
{
    private const string MotDePasse = "Correct#2026";
    private const string Ip = "10.0.0.1";

    [Fact]
    public async Task Connexion_reussie_renvoie_une_identite_avec_l_organisme()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG");
        var compte = await env.CreerUtilisateurAsync("agent", organisme, MotDePasse);

        var resultat = await env.Authentification().ConnecterAsync("agent", MotDePasse, Ip, "tests");

        Assert.True(resultat.Ok);
        Assert.Equal(compte.Id, resultat.Principal!.UtilisateurId());
        Assert.Equal(organisme, resultat.Principal!.OrganismeId());

        await using var ctx = env.Contexte();
        var journal = await ctx.JournalConnexions.SingleAsync();
        Assert.Equal(StatutConnexion.Succes, journal.Statut);
        Assert.Equal(Ip, journal.AdresseIp);
        Assert.NotNull((await ctx.Utilisateurs.SingleAsync()).DerniereConnexion);
    }

    [Fact]
    public async Task Compte_bloque_apres_N_echecs_meme_avec_le_bon_mot_de_passe()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG", nbTentativesMax: 3);
        await env.CreerUtilisateurAsync("agent", organisme, MotDePasse);
        var service = env.Authentification();

        for (var i = 0; i < 2; i++)
        {
            var echec = await service.ConnecterAsync("agent", "mauvais", Ip, null);
            Assert.False(echec.Ok);
            Assert.DoesNotContain("bloqué", echec.Erreur);
        }

        var troisieme = await service.ConnecterAsync("agent", "mauvais", Ip, null);
        Assert.False(troisieme.Ok);
        Assert.Contains("bloqué", troisieme.Erreur);

        // Pendant le blocage, même le bon mot de passe est refusé.
        var bonMotDePasse = await service.ConnecterAsync("agent", MotDePasse, Ip, null);
        Assert.False(bonMotDePasse.Ok);
        Assert.Contains("bloqué", bonMotDePasse.Erreur);

        await using var ctx = env.Contexte();
        var compte = await ctx.Utilisateurs.SingleAsync();
        Assert.True(compte.BloqueJusquAu > DateTime.UtcNow);

        var statuts = await ctx.JournalConnexions.OrderBy(j => j.DateHeure).Select(j => j.Statut).ToListAsync();
        Assert.Equal(
            [StatutConnexion.Echec, StatutConnexion.Echec, StatutConnexion.CompteBloque, StatutConnexion.CompteBloque],
            statuts);
    }

    [Fact]
    public async Task Une_connexion_reussie_remet_le_compteur_d_echecs_a_zero()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG", nbTentativesMax: 3);
        await env.CreerUtilisateurAsync("agent", organisme, MotDePasse);
        var service = env.Authentification();

        await service.ConnecterAsync("agent", "mauvais", Ip, null);
        await service.ConnecterAsync("agent", "mauvais", Ip, null);
        Assert.True((await service.ConnecterAsync("agent", MotDePasse, Ip, null)).Ok);

        await using var ctx = env.Contexte();
        Assert.Equal(0, (await ctx.Utilisateurs.SingleAsync()).NbEchecsConnexion);
    }

    [Fact]
    public async Task Identifiant_inconnu_et_mot_de_passe_faux_donnent_le_meme_message()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG");
        await env.CreerUtilisateurAsync("agent", organisme, MotDePasse);
        var service = env.Authentification();

        var inconnu = await service.ConnecterAsync("personne", MotDePasse, Ip, null);
        var faux = await service.ConnecterAsync("agent", "mauvais", Ip, null);

        Assert.Equal(inconnu.Erreur, faux.Erreur);
    }

    [Fact]
    public async Task Organisme_suspendu_refuse_la_connexion()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG", statut: StatutOrganisme.Suspendu);
        await env.CreerUtilisateurAsync("agent", organisme, MotDePasse);

        var resultat = await env.Authentification().ConnecterAsync("agent", MotDePasse, Ip, null);

        Assert.False(resultat.Ok);
        Assert.Contains("suspendu", resultat.Erreur);
    }

    [Fact]
    public async Task Session_refusee_apres_changement_de_mot_de_passe_ou_desactivation()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG");
        var compte = await env.CreerUtilisateurAsync("agent", organisme, MotDePasse);
        var principal = (await env.Authentification().ConnecterAsync("agent", MotDePasse, Ip, null)).Principal!;

        Assert.True(await env.Authentification().ValiderSessionAsync(principal));

        await using (var ctx = env.Contexte())
        {
            var u = await ctx.Utilisateurs.SingleAsync();
            u.PasswordHash = env.Hacheur.HashPassword(u, "Autre#Secret99");
            await ctx.SaveChangesAsync();
        }
        Assert.False(await env.Authentification().ValiderSessionAsync(principal));

        // Nouvelle connexion, puis désactivation du compte.
        principal = (await env.Authentification().ConnecterAsync("agent", "Autre#Secret99", Ip, null)).Principal!;
        await using (var ctx = env.Contexte())
        {
            (await ctx.Utilisateurs.SingleAsync(u => u.Id == compte.Id)).EstActif = false;
            await ctx.SaveChangesAsync();
        }
        Assert.False(await env.Authentification().ValiderSessionAsync(principal));
    }

    [Theory]
    [InlineData("court#A1", false)]
    [InlineData("sansmajuscule#1", false)]
    [InlineData("SansChiffre#ici", false)]
    [InlineData("SansSpecial123", false)]
    [InlineData("Conforme#2026", true)]
    public void Politique_de_mot_de_passe(string motDePasse, bool conforme)
        => Assert.Equal(conforme, PolitiqueMotDePasse.Verifier(motDePasse, new PolitiqueMotDePasseOptions()) is null);
}
