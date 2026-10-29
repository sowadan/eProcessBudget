using eProcessBudget.Core.Entities;
using eProcessBudget.Core.Enums;
using eProcessBudget.Infrastructure.Data;
using eProcessBudget.Infrastructure.Services.Audit;
using eProcessBudget.Infrastructure.Services.Securite;
using eProcessBudget.Tests.Outils;
using Microsoft.EntityFrameworkCore;

namespace eProcessBudget.Tests;

/// <summary>Isolation entre organismes (cahier technique § 2.3, test clé du § 4.1).</summary>
public class CloisonnementTests
{
    [Fact]
    public async Task Un_organisme_ne_lit_que_ses_propres_donnees()
    {
        var env = new Environnement();
        var a = await env.CreerOrganismeAsync("A");
        var b = await env.CreerOrganismeAsync("B");

        await using (var ctx = env.Contexte(a))
        {
            ctx.Axes.Add(new Axe { Code = "AXE-A", Libelle = "Axe de A" });
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = env.Contexte(b))
        {
            ctx.Axes.Add(new Axe { Code = "AXE-B", Libelle = "Axe de B" });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = env.Contexte(a))
            Assert.Equal(["AXE-A"], await ctx.Axes.Select(x => x.Code).ToListAsync());

        await using (var ctx = env.Contexte(b))
            Assert.Equal(["AXE-B"], await ctx.Axes.Select(x => x.Code).ToListAsync());

        // Sans organisme courant (oubli, compte plateforme) : rien du tout.
        await using (var ctx = env.Contexte())
            Assert.Empty(await ctx.Axes.ToListAsync());
    }

    [Fact]
    public async Task Un_organisme_ne_peut_pas_modifier_les_donnees_d_un_autre()
    {
        var env = new Environnement();
        var a = await env.CreerOrganismeAsync("A");
        var b = await env.CreerOrganismeAsync("B");

        await using (var ctx = env.Contexte(a))
        {
            ctx.Axes.Add(new Axe { Code = "AXE-A", Libelle = "Axe de A" });
            await ctx.SaveChangesAsync();
        }

        await using var ctxB = env.Contexte(b);
        var axeDeA = await ctxB.Axes.IgnoreQueryFilters().SingleAsync();
        axeDeA.Libelle = "Modifié par B";

        await Assert.ThrowsAsync<InvalidOperationException>(() => ctxB.SaveChangesAsync());
    }

    [Fact]
    public async Task La_fabrique_renseigne_l_organisme_de_l_utilisateur_connecte()
    {
        var env = new Environnement();
        var organisme = await env.CreerOrganismeAsync("ORG");
        var compte = await env.CreerUtilisateurAsync("agent", organisme, "Correct#2026");
        var principal = (await env.Authentification().ConnecterAsync("agent", "Correct#2026", "ip", null)).Principal!;

        var fabrique = new FabriqueContexteOrganisme(env.Fabrique(), new PrincipalFactice(principal), new ContexteConnexion());

        await using var ctx = await fabrique.CreateDbContextAsync();
        Assert.Equal(compte.OrganismeId, ctx.OrganismeCourantId);

        // La version synchrone bloquerait le circuit : elle est refusée.
        Assert.Throws<NotSupportedException>(() => fabrique.CreateDbContext());
    }

    [Fact]
    public void Un_compte_plateforme_n_a_aucun_droit_budgetaire_et_un_organisme_aucun_droit_plateforme()
    {
        var tous = Enum.GetValues<TypeDroit>();

        var plateforme = DroitHelper.FiltrerSelonNiveau(tous, estAdministrateurPlateforme: true).ToList();
        Assert.Contains(TypeDroit.OrganismeGerer, plateforme);
        Assert.DoesNotContain(TypeDroit.EngagementSaisir, plateforme);
        Assert.DoesNotContain(TypeDroit.TableauDeBordLire, plateforme);

        var organisme = DroitHelper.FiltrerSelonNiveau(tous, estAdministrateurPlateforme: false).ToList();
        Assert.DoesNotContain(TypeDroit.OrganismeGerer, organisme);
        Assert.DoesNotContain(TypeDroit.ProfilGestion, organisme);
        Assert.Contains(TypeDroit.EngagementSaisir, organisme);
    }
}
