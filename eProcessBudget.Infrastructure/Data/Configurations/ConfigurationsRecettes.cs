using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - recettes (schéma rec)
// ═══════════════════════════════════════════════════════════════════════════════

public class PrevisionRecetteConfiguration : IEntityTypeConfiguration<PrevisionRecette>
{
    public void Configure(EntityTypeBuilder<PrevisionRecette> e)
    {
        e.ToTable("PrevisionRecette", "rec");
        e.Property(x => x.MotifRevision).HasMaxLength(500);
        e.HasIndex(x => new { x.ExerciceId, x.NatureRecetteId, x.SiteId });
    }
}

public class EncaissementConfiguration : IEntityTypeConfiguration<Encaissement>
{
    public void Configure(EntityTypeBuilder<Encaissement> e)
    {
        e.ToTable("Encaissement", "rec", t => t.HasCheckConstraint("CK_Encaissement_Montant", "[Montant] >= 0"));
        e.Property(x => x.Reference).HasMaxLength(100);
        e.HasOne(x => x.PrevisionRecette).WithMany(p => p.Encaissements).HasForeignKey(x => x.PrevisionRecetteId);
        e.HasIndex(x => new { x.ExerciceId, x.PeriodeId });
    }
}

public class EmissionRecetteConfiguration : IEntityTypeConfiguration<EmissionRecette>
{
    public void Configure(EntityTypeBuilder<EmissionRecette> e)
    {
        e.ToTable("EmissionRecette", "rec", t => t.HasCheckConstraint("CK_EmissionRecette_Montant", "[Montant] >= 0"));
        e.Property(x => x.Reference).HasMaxLength(100);
        e.HasOne(x => x.PrevisionRecette).WithMany(p => p.Emissions).HasForeignKey(x => x.PrevisionRecetteId);
        e.HasIndex(x => new { x.ExerciceId, x.PeriodeId });
    }
}

public class EcheanceRecouvrementConfiguration : IEntityTypeConfiguration<EcheanceRecouvrement>
{
    public void Configure(EntityTypeBuilder<EcheanceRecouvrement> e)
    {
        e.ToTable("EcheanceRecouvrement", "rec");
        e.HasOne(x => x.PrevisionRecette).WithMany(p => p.Echeances).HasForeignKey(x => x.PrevisionRecetteId);
        e.HasIndex(x => new { x.OrganismeId, x.DateEcheance });
    }
}

public class ReportRessourceConfiguration : IEntityTypeConfiguration<ReportRessource>
{
    public void Configure(EntityTypeBuilder<ReportRessource> e)
    {
        e.ToTable("ReportRessource", "rec", t => t.HasCheckConstraint("CK_ReportRessource_MontantPrevu", "[MontantPrevu] IS NULL OR [MontantPrevu] >= 0"));
        e.Property(x => x.Commentaire).HasMaxLength(500);
    }
}
