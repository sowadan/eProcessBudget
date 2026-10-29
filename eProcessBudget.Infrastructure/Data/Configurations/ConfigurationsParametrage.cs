using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - paramètres généraux (schéma cfg)
// ═══════════════════════════════════════════════════════════════════════════════

public class ParametreOrganismeConfiguration : IEntityTypeConfiguration<ParametreOrganisme>
{
    public void Configure(EntityTypeBuilder<ParametreOrganisme> e)
    {
        e.ToTable("ParametreOrganisme", "cfg", t =>
        {
            t.HasCheckConstraint("CK_ParametreOrganisme_Retention", "[RetentionJournauxMois] >= 12");
            t.HasCheckConstraint("CK_ParametreOrganisme_Decimales", "[NbDecimales] BETWEEN 0 AND 4");
        });
        e.Property(x => x.EnteteRapport).HasMaxLength(500);
        e.Property(x => x.PiedPageRapport).HasMaxLength(500);
        e.HasIndex(x => x.OrganismeId).IsUnique();
    }
}

public class FichierConfiguration : IEntityTypeConfiguration<Fichier>
{
    public void Configure(EntityTypeBuilder<Fichier> e)
    {
        e.ToTable("Fichier", "cfg");
        e.Property(x => x.NomFichier).HasMaxLength(255);
        e.Property(x => x.ContentType).HasMaxLength(100);
        e.Property(x => x.Empreinte).HasMaxLength(64);
        e.Property(x => x.Contenu).HasColumnType("varbinary(max)");
    }
}

public class DeviseConfiguration : IEntityTypeConfiguration<Devise>
{
    public void Configure(EntityTypeBuilder<Devise> e)
    {
        e.ToTable("Devise", "cfg");
        e.Property(x => x.CodeIso).HasMaxLength(3);
        e.Property(x => x.Libelle).HasMaxLength(50);
        e.Property(x => x.Symbole).HasMaxLength(10);
        e.HasIndex(x => new { x.OrganismeId, x.CodeIso }).IsUnique();
    }
}

public class TauxChangeConfiguration : IEntityTypeConfiguration<TauxChange>
{
    public void Configure(EntityTypeBuilder<TauxChange> e)
    {
        e.ToTable("TauxChange", "cfg");
        e.Property(x => x.Taux).EnValeur();
        e.HasIndex(x => new { x.DeviseId, x.DateEffet }).IsUnique();
    }
}

public class CycleProgrammationConfiguration : IEntityTypeConfiguration<CycleProgrammation>
{
    public void Configure(EntityTypeBuilder<CycleProgrammation> e)
    {
        e.ToTable("CycleProgrammation", "cfg", t => t.HasCheckConstraint("CK_CycleProgrammation_Annees",
            "[AnneeReference] = [AnneeBase] - 1 AND [AnneeFin] = [AnneeBase] + 2"));
        e.Property(x => x.Libelle).HasMaxLength(100);
        e.HasIndex(x => new { x.OrganismeId, x.AnneeBase }).IsUnique();
    }
}

public class ExerciceConfiguration : IEntityTypeConfiguration<Exercice>
{
    public void Configure(EntityTypeBuilder<Exercice> e)
    {
        e.ToTable("Exercice", "cfg", t => t.HasCheckConstraint("CK_Exercice_Dates", "[DateFin] > [DateDebut]"));
        e.ReferenceUtilisateur(x => x.ClotureParId);
        e.HasIndex(x => new { x.OrganismeId, x.Annee }).IsUnique();
    }
}

public class PeriodeConfiguration : IEntityTypeConfiguration<Periode>
{
    public void Configure(EntityTypeBuilder<Periode> e)
    {
        e.ToTable("Periode", "cfg");
        e.HasIndex(x => new { x.ExerciceId, x.TypePeriode, x.Numero }).IsUnique();
    }
}

public class SeuilAlerteConfiguration : IEntityTypeConfiguration<SeuilAlerte>
{
    public void Configure(EntityTypeBuilder<SeuilAlerte> e)
    {
        e.ToTable("SeuilAlerte", "cfg");
        e.Property(x => x.Pourcentage).EnPourcentage();
        e.Property(x => x.Couleur).HasMaxLength(7);
    }
}
