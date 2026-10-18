using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Delivery;
using Pmo.Domain.Portfolio;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ProgressSnapshotConfiguration : IEntityTypeConfiguration<ProgressSnapshot>
{
    public void Configure(EntityTypeBuilder<ProgressSnapshot> builder)
    {
        builder.ToTable("progress_snapshots");
        builder.Property(x => x.ProgressPct).HasPrecision(5, 2);
        builder.Property(x => x.ActualSpend).HasPrecision(18, 2);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProjectId, x.SnapshotDate }).IsUnique();
    }
}
