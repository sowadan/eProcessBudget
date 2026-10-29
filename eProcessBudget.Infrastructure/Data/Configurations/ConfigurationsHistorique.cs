using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - historique (schéma hist)
// ═══════════════════════════════════════════════════════════════════════════════

public class ExecutionHistoriqueConfiguration : IEntityTypeConfiguration<ExecutionHistorique>
{
    public void Configure(EntityTypeBuilder<ExecutionHistorique> e)
    {
        e.ToTable("ExecutionHistorique", "hist");
        e.Property(x => x.KmEntretenus).EnValeur();
        e.HasIndex(x => new { x.OrganismeId, x.Annee });
    }
}
