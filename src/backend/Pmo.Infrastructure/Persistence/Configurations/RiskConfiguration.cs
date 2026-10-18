using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Delivery;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> builder)
    {
        builder.ToTable("risks", t =>
        {
            t.HasCheckConstraint("ck_risks_type", SqlCheck.In("type", RiskType.All));
            t.HasCheckConstraint("ck_risks_status", SqlCheck.In("status", RiskStatus.All));
            t.HasCheckConstraint("ck_risks_probability", "probability BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_risks_impact", "impact BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_risks_closed_at", "(status = 'closed') = (closed_at IS NOT NULL)");
        });

        builder.Property(x => x.Type).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();

        // GENERATED ALWAYS AS (probability * impact) STORED: PostgreSQL computes it, EF only reads it.
        builder.Property(x => x.Score).HasComputedColumnSql("probability * impact", stored: true);
        AuditColumns.Configure(builder);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProjectId, x.Status, x.Score });
    }
}
