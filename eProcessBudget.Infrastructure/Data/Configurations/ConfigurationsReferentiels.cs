using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - référentiels du Paramétrage (schéma ref)
// ═══════════════════════════════════════════════════════════════════════════════

public class ListeValeurConfiguration : IEntityTypeConfiguration<ListeValeur>
{
    public void Configure(EntityTypeBuilder<ListeValeur> e)
    {
        e.ToTable("ListeValeur", "ref");
        e.Property(x => x.Categorie).HasMaxLength(40);
        e.Property(x => x.Code).HasMaxLength(30);
        e.Property(x => x.Libelle).HasMaxLength(200);
        e.HasIndex(x => new { x.OrganismeId, x.Categorie, x.Code }).IsUnique();
    }
}

public class AxeConfiguration : IEntityTypeConfiguration<Axe>
{
    public void Configure(EntityTypeBuilder<Axe> e)
    {
        e.ToTable("Axe", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(100);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class StructureConfiguration : IEntityTypeConfiguration<Structure>
{
    public void Configure(EntityTypeBuilder<Structure> e)
    {
        e.ToTable("Structure", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Sigle).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(200);
        e.Property(x => x.ResponsableNom).HasMaxLength(150);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> e)
    {
        e.ToTable("Site", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Nom).HasMaxLength(150);
        e.Property(x => x.Region).HasMaxLength(100);
        e.Property(x => x.Prefecture).HasMaxLength(100);
        e.Property(x => x.Commune).HasMaxLength(100);
        e.Property(x => x.AxeRoutier).HasMaxLength(100);
        e.HasOne(x => x.CompteTiers).WithMany().HasForeignKey(x => x.CompteTiersId);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class SiteTiersConfiguration : IEntityTypeConfiguration<SiteTiers>
{
    public void Configure(EntityTypeBuilder<SiteTiers> e)
    {
        e.ToTable("SiteTiers", "ref");
        e.HasOne(x => x.Site).WithMany(s => s.Collectivites).HasForeignKey(x => x.SiteId);
        // Réponse C.2.8 : plusieurs collectivités par poste, mais un seul poste par collectivité.
        e.HasIndex(x => x.TiersId).IsUnique();
        e.HasIndex(x => x.SiteId);
    }
}

public class CategorieEmploiConfiguration : IEntityTypeConfiguration<CategorieEmploi>
{
    public void Configure(EntityTypeBuilder<CategorieEmploi> e)
    {
        e.ToTable("CategorieEmploi", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(100);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> e)
    {
        e.ToTable("Agent", "ref");
        e.Property(x => x.Matricule).HasMaxLength(30);
        e.Property(x => x.NomComplet).HasMaxLength(200);
        e.HasIndex(x => new { x.OrganismeId, x.Matricule }).IsUnique();
    }
}

public class NatureDepenseConfiguration : IEntityTypeConfiguration<NatureDepense>
{
    public void Configure(EntityTypeBuilder<NatureDepense> e)
    {
        e.ToTable("NatureDepense", "ref");
        e.Property(x => x.Code).HasMaxLength(10);
        e.Property(x => x.Libelle).HasMaxLength(150);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class SourceFinancementConfiguration : IEntityTypeConfiguration<SourceFinancement>
{
    public void Configure(EntityTypeBuilder<SourceFinancement> e)
    {
        e.ToTable("SourceFinancement", "ref");
        e.Property(x => x.Code).HasMaxLength(10);
        e.Property(x => x.Libelle).HasMaxLength(150);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class CompteComptableConfiguration : IEntityTypeConfiguration<CompteComptable>
{
    public void Configure(EntityTypeBuilder<CompteComptable> e)
    {
        e.ToTable("CompteComptable", "ref", t => t.HasCheckConstraint("CK_CompteComptable_Classe", "[Classe] BETWEEN 1 AND 9"));
        e.Property(x => x.Numero).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(250);
        e.HasIndex(x => new { x.OrganismeId, x.Numero }).IsUnique();
    }
}

public class RubriqueConfiguration : IEntityTypeConfiguration<Rubrique>
{
    public void Configure(EntityTypeBuilder<Rubrique> e)
    {
        e.ToTable("Rubrique", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Note).HasMaxLength(5);
        e.Property(x => x.Libelle).HasMaxLength(250);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class SousRubriqueConfiguration : IEntityTypeConfiguration<SousRubrique>
{
    public void Configure(EntityTypeBuilder<SousRubrique> e)
    {
        e.ToTable("SousRubrique", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(250);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class LigneBudgetaireConfiguration : IEntityTypeConfiguration<LigneBudgetaire>
{
    public void Configure(EntityTypeBuilder<LigneBudgetaire> e)
    {
        e.ToTable("LigneBudgetaire", "ref", t => t.HasCheckConstraint("CK_LigneBudgetaire_Nature",
            "([Sens] = 1 AND [NatureDepenseId] IS NOT NULL) OR ([Sens] = 2 AND [NatureRecetteId] IS NOT NULL)"));
        e.Property(x => x.Code).HasMaxLength(30);
        e.Property(x => x.Libelle).HasMaxLength(250);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class LigneCompteConfiguration : IEntityTypeConfiguration<LigneCompte>
{
    public void Configure(EntityTypeBuilder<LigneCompte> e)
    {
        e.ToTable("LigneCompte", "ref");
        e.HasOne(x => x.LigneBudgetaire).WithMany(l => l.Comptes).HasForeignKey(x => x.LigneBudgetaireId);
        e.HasIndex(x => new { x.LigneBudgetaireId, x.CompteComptableId }).IsUnique();
    }
}

public class MotifAjustementConfiguration : IEntityTypeConfiguration<MotifAjustement>
{
    public void Configure(EntityTypeBuilder<MotifAjustement> e)
    {
        e.ToTable("MotifAjustement", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(200);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class UniteMesureConfiguration : IEntityTypeConfiguration<UniteMesure>
{
    public void Configure(EntityTypeBuilder<UniteMesure> e)
    {
        e.ToTable("UniteMesure", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(100);
        e.Property(x => x.Symbole).HasMaxLength(10);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class RisqueConfiguration : IEntityTypeConfiguration<Risque>
{
    public void Configure(EntityTypeBuilder<Risque> e)
    {
        e.ToTable("Risque", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(300);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class TypeTravauxConfiguration : IEntityTypeConfiguration<TypeTravaux>
{
    public void Configure(EntityTypeBuilder<TypeTravaux> e)
    {
        e.ToTable("TypeTravaux", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(200);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class TiersConfiguration : IEntityTypeConfiguration<Tiers>
{
    public void Configure(EntityTypeBuilder<Tiers> e)
    {
        e.ToTable("Tiers", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.RaisonSociale).HasMaxLength(250);
        e.Property(x => x.Sigle).HasMaxLength(30);
        e.Property(x => x.IdentifiantFiscal).HasMaxLength(30);
        e.Property(x => x.Adresse).HasMaxLength(300);
        e.Property(x => x.Telephone).HasMaxLength(30);
        e.Property(x => x.Email).HasMaxLength(150);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
        e.HasIndex(x => new { x.OrganismeId, x.TypeTiers });
    }
}

public class NatureRecetteConfiguration : IEntityTypeConfiguration<NatureRecette>
{
    public void Configure(EntityTypeBuilder<NatureRecette> e)
    {
        e.ToTable("NatureRecette", "ref");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(200);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class CompteBancaireConfiguration : IEntityTypeConfiguration<CompteBancaire>
{
    public void Configure(EntityTypeBuilder<CompteBancaire> e)
    {
        e.ToTable("CompteBancaire", "ref");
        e.Property(x => x.Banque).HasMaxLength(150);
        e.Property(x => x.Intitule).HasMaxLength(200);
        e.Property(x => x.NumeroCompte).HasMaxLength(50);
        e.HasIndex(x => new { x.OrganismeId, x.NumeroCompte }).IsUnique();
    }
}
