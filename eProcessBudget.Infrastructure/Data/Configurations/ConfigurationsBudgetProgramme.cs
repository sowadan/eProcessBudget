using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - budget-programme et performance (schéma prog)
// ═══════════════════════════════════════════════════════════════════════════════

public class ProgrammeConfiguration : IEntityTypeConfiguration<Programme>
{
    public void Configure(EntityTypeBuilder<Programme> e)
    {
        e.ToTable("Programme", "prog");
        e.Property(x => x.Code).HasMaxLength(10);
        e.Property(x => x.Libelle).HasMaxLength(250);
        e.Property(x => x.ObjectifGeneral).HasMaxLength(500);
        e.Property(x => x.Presentation).EnTexteLong();
        e.ReferenceUtilisateur(x => x.ResponsableId);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class ProgrammeStructureConfiguration : IEntityTypeConfiguration<ProgrammeStructure>
{
    public void Configure(EntityTypeBuilder<ProgrammeStructure> e)
    {
        e.ToTable("ProgrammeStructure", "prog");
        e.HasOne(x => x.Programme).WithMany(p => p.Structures).HasForeignKey(x => x.ProgrammeId);
        e.HasIndex(x => new { x.ProgrammeId, x.StructureId }).IsUnique();
    }
}

public class ActionProgrammeConfiguration : IEntityTypeConfiguration<ActionProgramme>
{
    public void Configure(EntityTypeBuilder<ActionProgramme> e)
    {
        e.ToTable("Action", "prog");
        e.Property(x => x.Code).HasMaxLength(10);
        e.Property(x => x.Libelle).HasMaxLength(250);
        e.Property(x => x.ObjectifSpecifique).HasMaxLength(500);
        e.HasOne(x => x.Programme).WithMany(p => p.Actions).HasForeignKey(x => x.ProgrammeId);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class ActiviteConfiguration : IEntityTypeConfiguration<Activite>
{
    public void Configure(EntityTypeBuilder<Activite> e)
    {
        e.ToTable("Activite", "prog");
        e.Property(x => x.Code).HasMaxLength(15);
        e.Property(x => x.Libelle).HasMaxLength(250);
        e.Property(x => x.Description).HasMaxLength(1000);
        e.HasOne(x => x.Action).WithMany(a => a.Activites).HasForeignKey(x => x.ActionId);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class CleRepartitionConfiguration : IEntityTypeConfiguration<CleRepartition>
{
    public void Configure(EntityTypeBuilder<CleRepartition> e)
    {
        e.ToTable("CleRepartition", "prog", t => t.HasCheckConstraint("CK_CleRepartition_Pourcentage",
            "[Pourcentage] > 0 AND [Pourcentage] <= 100"));
        e.Property(x => x.Pourcentage).EnPourcentage();
        e.HasIndex(x => new { x.ExerciceId, x.LigneBudgetaireId, x.ActiviteId }).IsUnique();
    }
}

public class IndicateurConfiguration : IEntityTypeConfiguration<Indicateur>
{
    public void Configure(EntityTypeBuilder<Indicateur> e)
    {
        e.ToTable("Indicateur", "prog", t => t.HasCheckConstraint("CK_Indicateur_Rattachement",
            "([Niveau] = 1 AND [ProgrammeId] IS NOT NULL AND [ActionId] IS NULL) OR ([Niveau] IN (2, 3) AND [ActionId] IS NOT NULL)"));
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.Libelle).HasMaxLength(300);
        e.Property(x => x.ModeCalcul).HasMaxLength(1000);
        e.Property(x => x.CodeFormule).HasMaxLength(50);
        e.Property(x => x.ValeurReference).EnValeur();
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class IndicateurCibleConfiguration : IEntityTypeConfiguration<IndicateurCible>
{
    public void Configure(EntityTypeBuilder<IndicateurCible> e)
    {
        e.ToTable("IndicateurCible", "prog");
        e.Property(x => x.ValeurCible).EnValeur();
        e.HasOne(x => x.Indicateur).WithMany(i => i.Cibles).HasForeignKey(x => x.IndicateurId);
        e.HasIndex(x => new { x.IndicateurId, x.CycleProgrammationId, x.Annee }).IsUnique();
    }
}

public class IndicateurRealisationConfiguration : IEntityTypeConfiguration<IndicateurRealisation>
{
    public void Configure(EntityTypeBuilder<IndicateurRealisation> e)
    {
        e.ToTable("IndicateurRealisation", "prog");
        e.Property(x => x.ValeurRealisee).EnValeur();
        e.Property(x => x.Commentaire).HasMaxLength(1000);
        e.Property(x => x.JustificationEcart).HasMaxLength(1000);
        e.HasOne(x => x.Indicateur).WithMany(i => i.Realisations).HasForeignKey(x => x.IndicateurId);
        e.ReferenceUtilisateur(x => x.TransmisParId);
        e.HasIndex(x => new { x.IndicateurId, x.PeriodeId }).IsUnique();
    }
}

public class CadreLogiqueElementConfiguration : IEntityTypeConfiguration<CadreLogiqueElement>
{
    public void Configure(EntityTypeBuilder<CadreLogiqueElement> e)
    {
        e.ToTable("CadreLogiqueElement", "prog");
        e.Property(x => x.Libelle).HasMaxLength(500);
        e.Property(x => x.ValeurCible).EnValeur();
        e.HasIndex(x => new { x.ProgrammeId, x.CycleProgrammationId });
    }
}

public class CadreLogiqueRisqueConfiguration : IEntityTypeConfiguration<CadreLogiqueRisque>
{
    public void Configure(EntityTypeBuilder<CadreLogiqueRisque> e)
    {
        e.ToTable("CadreLogiqueRisque", "prog");
        e.HasOne(x => x.CadreLogiqueElement).WithMany(c => c.Risques).HasForeignKey(x => x.CadreLogiqueElementId);
        e.HasIndex(x => new { x.CadreLogiqueElementId, x.RisqueId }).IsUnique();
    }
}

public class ProgrammationFinanciereConfiguration : IEntityTypeConfiguration<ProgrammationFinanciere>
{
    public void Configure(EntityTypeBuilder<ProgrammationFinanciere> e)
    {
        e.ToTable("ProgrammationFinanciere", "prog");
        e.HasIndex(x => new { x.CycleProgrammationId, x.Annee, x.ActionId });
    }
}

public class ProgrammationRessourceConfiguration : IEntityTypeConfiguration<ProgrammationRessource>
{
    public void Configure(EntityTypeBuilder<ProgrammationRessource> e)
    {
        e.ToTable("ProgrammationRessource", "prog");
        e.HasIndex(x => new { x.CycleProgrammationId, x.Annee, x.NatureRecetteId, x.SourceFinancementId }).IsUnique();
    }
}

public class ProgrammationEmploiConfiguration : IEntityTypeConfiguration<ProgrammationEmploi>
{
    public void Configure(EntityTypeBuilder<ProgrammationEmploi> e)
    {
        e.ToTable("ProgrammationEmploi", "prog", t => t.HasCheckConstraint("CK_ProgrammationEmploi_Effectif", "[Effectif] >= 0"));
        e.HasIndex(x => new { x.CycleProgrammationId, x.Annee, x.ProgrammeId, x.CategorieEmploiId }).IsUnique();
    }
}

public class RepartitionTempsConfiguration : IEntityTypeConfiguration<RepartitionTemps>
{
    public void Configure(EntityTypeBuilder<RepartitionTemps> e)
    {
        e.ToTable("RepartitionTemps", "prog", t => t.HasCheckConstraint("CK_RepartitionTemps_Pourcentage",
            "[PourcentageTemps] > 0 AND [PourcentageTemps] <= 100"));
        e.Property(x => x.PourcentageTemps).EnPourcentage();
        e.HasIndex(x => new { x.ExerciceId, x.AgentId, x.ActionId, x.ActiviteId }).IsUnique();
    }
}

public class AnalyseQualitativeConfiguration : IEntityTypeConfiguration<AnalyseQualitative>
{
    public void Configure(EntityTypeBuilder<AnalyseQualitative> e)
    {
        e.ToTable("AnalyseQualitative", "prog");
        e.Property(x => x.Contenu).EnTexteLong();
        e.HasIndex(x => new { x.ExerciceId, x.TypeAnalyse });
    }
}
