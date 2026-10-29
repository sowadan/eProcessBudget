using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - plateforme et habilitations (schémas plat, auth)
// ═══════════════════════════════════════════════════════════════════════════════

public class OrganismeConfiguration : IEntityTypeConfiguration<Organisme>
{
    public void Configure(EntityTypeBuilder<Organisme> e)
    {
        e.ToTable("Organisme", "plat");
        e.Property(x => x.Code).HasMaxLength(20);
        e.Property(x => x.RaisonSociale).HasMaxLength(200);
        e.Property(x => x.Sigle).HasMaxLength(30);
        e.Property(x => x.Adresse).HasMaxLength(300);
        e.Property(x => x.Telephone).HasMaxLength(30);
        e.Property(x => x.Email).HasMaxLength(150);
        e.Property(x => x.TutelleTechnique).HasMaxLength(200);
        e.Property(x => x.TutelleFinanciere).HasMaxLength(200);
        e.HasIndex(x => x.Code).IsUnique();
    }
}

public class HabilitationConfiguration : IEntityTypeConfiguration<Habilitation>
{
    public void Configure(EntityTypeBuilder<Habilitation> e)
    {
        e.ToTable("Habilitation", "auth", t => t.HasCheckConstraint("CK_Habilitation_ValideursDistincts",
            "[ValideurFinancierId] IS NULL OR [ValideurAdminSIId] IS NULL OR [ValideurFinancierId] <> [ValideurAdminSIId]"));
        e.Property(x => x.MotifRejet).HasMaxLength(500);

        e.HasOne(x => x.Utilisateur).WithMany().HasForeignKey(x => x.UtilisateurId);
        e.ReferenceUtilisateur(x => x.DemandeParId);
        e.ReferenceUtilisateur(x => x.ValideurFinancierId);
        e.ReferenceUtilisateur(x => x.ValideurAdminSIId);

        // File des habilitations à valider et contrôle des droits actifs d'un utilisateur.
        e.HasIndex(x => new { x.OrganismeId, x.Statut });
    }
}

public class JournalConnexionConfiguration : IEntityTypeConfiguration<JournalConnexion>
{
    public void Configure(EntityTypeBuilder<JournalConnexion> e)
    {
        e.ToTable("JournalConnexion", "auth");
        e.Property(x => x.Login).HasMaxLength(150);
        e.Property(x => x.AdresseIp).HasMaxLength(45);
        e.Property(x => x.UserAgent).HasMaxLength(400);
        e.Property(x => x.Motif).HasMaxLength(200);
        e.Property(x => x.DateHeure).HasDefaultValueSql("SYSUTCDATETIME()");

        e.HasOne<Organisme>().WithMany().HasForeignKey(x => x.OrganismeId);
        e.ReferenceUtilisateur(x => x.UtilisateurId);

        e.HasIndex(x => x.DateHeure);
        e.HasIndex(x => new { x.UtilisateurId, x.DateHeure });
    }
}
