using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Services.Plateforme;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Tests;

/// <summary>
/// Principe des quatre yeux (cahier fonctionnel § 3.1.2, tests clés du § 4.1) : une
/// habilitation n'est active qu'après deux validations données par deux personnes distinctes.
/// </summary>
public class HabilitationTests
{
    private sealed record Scene(
        Environnement Env, HabilitationService Service, Ulid Organisme,
        Utilisateur Demandeur, Utilisateur Financier, Utilisateur AdminSI, Utilisateur Polyvalent, Utilisateur Beneficiaire);

    private static async Task<Scene> PreparerAsync()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG");

        var demandeur    = await env.CreerUtilisateurAsync("demandeur", organisme);
        var financier    = await env.CreerUtilisateurAsync("financier", organisme);
        var adminSI      = await env.CreerUtilisateurAsync("adminsi", organisme);
        var polyvalent   = await env.CreerUtilisateurAsync("polyvalent", organisme);
        var beneficiaire = await env.CreerUtilisateurAsync("beneficiaire", organisme);

        env.Droits.Accorder(demandeur.Id, TypeDroit.HabilitationDemander);
        env.Droits.Accorder(financier.Id, TypeDroit.HabilitationValiderFinancier);
        env.Droits.Accorder(adminSI.Id, TypeDroit.HabilitationValiderAdminSI);
        env.Droits.Accorder(polyvalent.Id, TypeDroit.HabilitationValiderFinancier, TypeDroit.HabilitationValiderAdminSI);
        env.Droits.Accorder(beneficiaire.Id, TypeDroit.HabilitationValiderFinancier, TypeDroit.HabilitationValiderAdminSI);

        var service = new HabilitationService(env.Fabrique(organisme), env.UtilisateurCourant, env.Droits,
                                              new AuditFactice(), new NotificationsFactices());

        return new Scene(env, service, organisme, demandeur, financier, adminSI, polyvalent, beneficiaire);
    }

    private static async Task<Ulid> DemanderAsync(Scene s)
    {
        s.Env.UtilisateurCourant.Utilisateur = s.Demandeur;
        var (ok, erreur) = await s.Service.DemanderAsync(new DemandeHabilitation
        {
            UtilisateurId = s.Beneficiaire.Id,
            TypePerimetre = TypePerimetre.OrganismeEntier,
            NiveauAcces = NiveauAcces.Saisie
        });
        Assert.True(ok, erreur);

        await using var ctx = s.Env.Contexte(s.Organisme);
        return (await ctx.Habilitations.SingleAsync()).Id;
    }

    private static async Task<StatutHabilitation> StatutAsync(Scene s, Ulid id)
    {
        await using var ctx = s.Env.Contexte(s.Organisme);
        return (await ctx.Habilitations.SingleAsync(h => h.Id == id)).Statut;
    }

    [Fact]
    public async Task Habilitation_inactive_tant_que_les_deux_validations_manquent()
    {
        var s = await PreparerAsync();
        var id = await DemanderAsync(s);

        Assert.Equal(StatutHabilitation.Demandee, await StatutAsync(s, id));
        Assert.Empty(await s.Service.ObtenirPerimetresActifsAsync(s.Beneficiaire.Id, NiveauAcces.Lecture));

        s.Env.UtilisateurCourant.Utilisateur = s.Financier;
        Assert.True((await s.Service.ValiderAsync(id, RoleValidation.ServiceFinancier)).Ok);
        Assert.Equal(StatutHabilitation.ValideeFinancier, await StatutAsync(s, id));
        Assert.Empty(await s.Service.ObtenirPerimetresActifsAsync(s.Beneficiaire.Id, NiveauAcces.Lecture));

        s.Env.UtilisateurCourant.Utilisateur = s.AdminSI;
        Assert.True((await s.Service.ValiderAsync(id, RoleValidation.AdministrateurSI)).Ok);
        Assert.Equal(StatutHabilitation.Active, await StatutAsync(s, id));

        var actifs = await s.Service.ObtenirPerimetresActifsAsync(s.Beneficiaire.Id, NiveauAcces.Saisie);
        Assert.Single(actifs);
        Assert.Equal(TypePerimetre.OrganismeEntier, actifs[0].TypePerimetre);
    }

    [Fact]
    public async Task La_meme_personne_ne_peut_pas_donner_les_deux_validations()
    {
        var s = await PreparerAsync();
        var id = await DemanderAsync(s);

        s.Env.UtilisateurCourant.Utilisateur = s.Polyvalent;
        Assert.True((await s.Service.ValiderAsync(id, RoleValidation.ServiceFinancier)).Ok);

        var (ok, erreur) = await s.Service.ValiderAsync(id, RoleValidation.AdministrateurSI);

        Assert.False(ok);
        Assert.Contains("autre personne", erreur);
        Assert.Equal(StatutHabilitation.ValideeFinancier, await StatutAsync(s, id));
    }

    [Fact]
    public async Task Le_beneficiaire_ne_valide_pas_sa_propre_habilitation()
    {
        var s = await PreparerAsync();
        var id = await DemanderAsync(s);

        s.Env.UtilisateurCourant.Utilisateur = s.Beneficiaire;
        var (ok, _) = await s.Service.ValiderAsync(id, RoleValidation.ServiceFinancier);

        Assert.False(ok);
        Assert.Equal(StatutHabilitation.Demandee, await StatutAsync(s, id));
    }

    [Fact]
    public async Task Sans_le_droit_de_validation_le_service_refuse()
    {
        var s = await PreparerAsync();
        var id = await DemanderAsync(s);

        // Le demandeur n'a que le droit de demander : le service refuse, quoi qu'affiche l'écran.
        var (ok, _) = await s.Service.ValiderAsync(id, RoleValidation.AdministrateurSI);

        Assert.False(ok);
        Assert.Equal(StatutHabilitation.Demandee, await StatutAsync(s, id));
    }

    [Fact]
    public async Task Rejet_et_revocation_exigent_un_motif()
    {
        var s = await PreparerAsync();
        var id = await DemanderAsync(s);
        s.Env.UtilisateurCourant.Utilisateur = s.Financier;

        Assert.False((await s.Service.RejeterAsync(id, "  ")).Ok);
        Assert.True((await s.Service.RejeterAsync(id, "Périmètre trop large")).Ok);
        Assert.Equal(StatutHabilitation.Rejetee, await StatutAsync(s, id));

        // Une demande rejetée ne peut plus être validée.
        Assert.False((await s.Service.ValiderAsync(id, RoleValidation.ServiceFinancier)).Ok);
    }

    [Fact]
    public async Task Demande_refusee_pour_un_beneficiaire_d_un_autre_organisme()
    {
        var s = await PreparerAsync();
        var autre = await s.Env.CreerOrganismeAsync("AUTRE");
        var etranger = await s.Env.CreerUtilisateurAsync("etranger", autre);

        s.Env.UtilisateurCourant.Utilisateur = s.Demandeur;
        var (ok, _) = await s.Service.DemanderAsync(new DemandeHabilitation
        {
            UtilisateurId = etranger.Id,
            TypePerimetre = TypePerimetre.OrganismeEntier,
            NiveauAcces = NiveauAcces.Lecture
        });

        Assert.False(ok);
    }
}
