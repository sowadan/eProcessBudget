using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - cycle budgétaire et exécution des dépenses (schéma bud)
// ═══════════════════════════════════════════════════════════════════════════════

public class ProjetConfiguration : IEntityTypeConfiguration<Projet>
{
    public void Configure(EntityTypeBuilder<Projet> e)
    {
        e.ToTable("Projet", "bud");
        e.Property(x => x.Code).HasMaxLength(30);
        e.Property(x => x.Intitule).HasMaxLength(300);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
        e.HasIndex(x => new { x.OrganismeId, x.AxeId, x.Statut });
    }
}

public class ProjetTravauxConfiguration : IEntityTypeConfiguration<ProjetTravaux>
{
    public void Configure(EntityTypeBuilder<ProjetTravaux> e)
    {
        e.ToTable("ProjetTravaux", "bud");
        e.Property(x => x.Localisation).HasMaxLength(300);
        e.Property(x => x.KmPrevus).EnValeur();

        // 1-1 : la clé primaire est aussi la clé étrangère vers bud.Projet.
        e.HasOne(x => x.Projet).WithOne(p => p.Travaux).HasForeignKey<ProjetTravaux>(x => x.Id);
    }
}

public class ContratConfiguration : IEntityTypeConfiguration<Contrat>
{
    public void Configure(EntityTypeBuilder<Contrat> e)
    {
        e.ToTable("Contrat", "bud");
        e.Property(x => x.Numero).HasMaxLength(50);
        e.Property(x => x.Objet).HasMaxLength(500);
        e.Property(x => x.KmContractuels).EnValeur();
        e.HasOne(x => x.Projet).WithMany(p => p.Contrats).HasForeignKey(x => x.ProjetId);
        e.HasIndex(x => new { x.OrganismeId, x.Numero }).IsUnique();
    }
}

public class DotationConfiguration : IEntityTypeConfiguration<Dotation>
{
    public void Configure(EntityTypeBuilder<Dotation> e)
    {
        e.ToTable("Dotation", "bud");
        e.HasIndex(x => new { x.ExerciceId, x.LigneBudgetaireId });
    }
}

public class AjustementConfiguration : IEntityTypeConfiguration<Ajustement>
{
    public void Configure(EntityTypeBuilder<Ajustement> e)
    {
        e.ToTable("Ajustement", "bud", t => t.HasCheckConstraint("CK_Ajustement_Montant", "[Montant] <> 0"));
        e.Property(x => x.Numero).HasMaxLength(30);
        e.Property(x => x.Justification).HasMaxLength(1000);
        e.HasOne(x => x.Dotation).WithMany(d => d.Ajustements).HasForeignKey(x => x.DotationId);
        e.ReferenceUtilisateur(x => x.ValideParId);
        e.HasIndex(x => new { x.OrganismeId, x.Numero }).IsUnique();
    }
}

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> e)
    {
        e.ToTable("Report", "bud");
        e.Property(x => x.Commentaire).HasMaxLength(500);
        e.HasIndex(x => new { x.ExerciceCibleId, x.TypeReport });
    }
}

public class EngagementConfiguration : IEntityTypeConfiguration<Engagement>
{
    public void Configure(EntityTypeBuilder<Engagement> e)
    {
        e.ToTable("Engagement", "bud", t => t.HasCheckConstraint("CK_Engagement_Montant", "[Montant] > 0"));
        e.Property(x => x.Numero).HasMaxLength(30);
        e.Property(x => x.Objet).HasMaxLength(500);
        e.HasOne(x => x.Dotation).WithMany(d => d.Engagements).HasForeignKey(x => x.DotationId);
        e.ReferenceUtilisateur(x => x.ValideParId);
        e.HasIndex(x => new { x.OrganismeId, x.Numero }).IsUnique();
        e.HasIndex(x => new { x.ExerciceId, x.Statut });
    }
}

public class EcheancierCPConfiguration : IEntityTypeConfiguration<EcheancierCP>
{
    public void Configure(EntityTypeBuilder<EcheancierCP> e)
    {
        e.ToTable("EcheancierCP", "bud");
        e.HasOne(x => x.Engagement).WithMany(g => g.EcheancierCP).HasForeignKey(x => x.EngagementId);
        e.HasIndex(x => new { x.EngagementId, x.Annee }).IsUnique();
    }
}

public class ValidationDepassementConfiguration : IEntityTypeConfiguration<ValidationDepassement>
{
    public void Configure(EntityTypeBuilder<ValidationDepassement> e)
    {
        e.ToTable("ValidationDepassement", "bud");
        e.Property(x => x.TauxApres).EnPourcentage();
        e.Property(x => x.Motif).HasMaxLength(1000);
        e.Property(x => x.CommentaireDecision).HasMaxLength(1000);

        // Navigations explicites : Dotation et Engagement ont déjà des collections
        // vers d'autres entités, on évite toute ambiguïté d'appariement.
        e.HasOne(x => x.Dotation).WithMany().HasForeignKey(x => x.DotationId);
        e.HasOne(x => x.Engagement).WithMany().HasForeignKey(x => x.EngagementId);
        e.ReferenceUtilisateur(x => x.DecideParId);

        // File des dépassements à valider.
        e.HasIndex(x => new { x.OrganismeId, x.Statut });
    }
}

public class FactureConfiguration : IEntityTypeConfiguration<Facture>
{
    public void Configure(EntityTypeBuilder<Facture> e)
    {
        e.ToTable("Facture", "bud", t => t.HasCheckConstraint("CK_Facture_Montant", "[Montant] > 0"));
        e.Property(x => x.Numero).HasMaxLength(50);
        e.HasOne(x => x.Engagement).WithMany(g => g.Factures).HasForeignKey(x => x.EngagementId);
        e.HasIndex(x => new { x.EngagementId, x.Numero }).IsUnique();
    }
}

public class PaiementConfiguration : IEntityTypeConfiguration<Paiement>
{
    public void Configure(EntityTypeBuilder<Paiement> e)
    {
        e.ToTable("Paiement", "bud", t => t.HasCheckConstraint("CK_Paiement_Montant", "[Montant] > 0"));
        e.Property(x => x.NumeroPiece).HasMaxLength(50);
        e.HasOne(x => x.Engagement).WithMany(g => g.Paiements).HasForeignKey(x => x.EngagementId);
        e.HasIndex(x => new { x.OrganismeId, x.NumeroPiece }).IsUnique();
        e.HasIndex(x => new { x.ExerciceId, x.DatePaiement });
    }
}

public class RejetPenaliteConfiguration : IEntityTypeConfiguration<RejetPenalite>
{
    public void Configure(EntityTypeBuilder<RejetPenalite> e)
    {
        e.ToTable("RejetPenalite", "bud", t => t.HasCheckConstraint("CK_RejetPenalite_Montant", "[Montant] > 0"));
        e.Property(x => x.Motif).HasMaxLength(500);
        e.HasOne(x => x.Engagement).WithMany(g => g.RejetsPenalites).HasForeignKey(x => x.EngagementId);
    }
}

public class AvancementPhysiqueConfiguration : IEntityTypeConfiguration<AvancementPhysique>
{
    public void Configure(EntityTypeBuilder<AvancementPhysique> e)
    {
        e.ToTable("AvancementPhysique", "bud");
        e.Property(x => x.KmRealises).EnValeur();
        e.Property(x => x.TauxAvancement).EnPourcentage();
        e.Property(x => x.Commentaire).HasMaxLength(1000);
        e.HasIndex(x => new { x.ProjetId, x.PeriodeId });
    }
}

public class BesoinEntretienConfiguration : IEntityTypeConfiguration<BesoinEntretien>
{
    public void Configure(EntityTypeBuilder<BesoinEntretien> e)
    {
        e.ToTable("BesoinEntretien", "bud", t => t.HasCheckConstraint("CK_BesoinEntretien_Montant", "[Montant] >= 0"));
        e.Property(x => x.Km).EnValeur();
        e.Property(x => x.Commentaire).HasMaxLength(1000);
        e.HasIndex(x => new { x.ExerciceId, x.TypeTravauxId });
    }
}

public class ImmobilisationConfiguration : IEntityTypeConfiguration<Immobilisation>
{
    public void Configure(EntityTypeBuilder<Immobilisation> e)
    {
        e.ToTable("Immobilisation", "bud");
        e.Property(x => x.Code).HasMaxLength(30);
        e.Property(x => x.Designation).HasMaxLength(300);
        e.HasIndex(x => new { x.OrganismeId, x.Code }).IsUnique();
    }
}

public class EnveloppeMaitreOuvrageConfiguration : IEntityTypeConfiguration<EnveloppeMaitreOuvrage>
{
    public void Configure(EntityTypeBuilder<EnveloppeMaitreOuvrage> e)
    {
        e.ToTable("EnveloppeMaitreOuvrage", "bud");
        e.HasIndex(x => new { x.ExerciceId, x.MaitreOuvrageId, x.RubriqueId }).IsUnique();
    }
}
