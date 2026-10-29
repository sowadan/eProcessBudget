using eProcessBudget.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eProcessBudget.Infrastructure.Data.Configurations;

// ═══════════════════════════════════════════════════════════════════════════════
//  Configurations EF Core - vues de calcul, en lecture seule (§ 3.5)
// ═══════════════════════════════════════════════════════════════════════════════

// Entités sans clé. EF Core ne crée pas les vues : leur SQL est écrit dans la migration
// VuesCalcul. Le filtre global d'organisme leur est appliqué comme aux tables.

public class SituationDotationConfiguration : IEntityTypeConfiguration<SituationDotation>
{
    public void Configure(EntityTypeBuilder<SituationDotation> e)
    {
        e.HasNoKey();
        e.ToView("vw_SituationDotation", "bud");
    }
}

public class ConsommationActiviteConfiguration : IEntityTypeConfiguration<ConsommationActivite>
{
    public void Configure(EntityTypeBuilder<ConsommationActivite> e)
    {
        e.HasNoKey();
        e.ToView("vw_ConsommationActivite", "prog");
        e.Property(x => x.Pourcentage).EnPourcentage();
    }
}

public class AtteinteIndicateurConfiguration : IEntityTypeConfiguration<AtteinteIndicateur>
{
    public void Configure(EntityTypeBuilder<AtteinteIndicateur> e)
    {
        e.HasNoKey();
        e.ToView("vw_AtteinteIndicateur", "prog");
        e.Property(x => x.ValeurRealisee).EnValeur();
        e.Property(x => x.ValeurCible).EnValeur();
        e.Property(x => x.EcartCible).EnValeur();
        e.Property(x => x.TauxAtteinte).EnValeur();
    }
}

public class SituationRecetteConfiguration : IEntityTypeConfiguration<SituationRecette>
{
    public void Configure(EntityTypeBuilder<SituationRecette> e)
    {
        e.HasNoKey();
        e.ToView("vw_SituationRecette", "rec");
        e.Property(x => x.TauxRecouvrement).EnValeur();
    }
}
