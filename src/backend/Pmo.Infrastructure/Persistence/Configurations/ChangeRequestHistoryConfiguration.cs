using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Governance;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ChangeRequestHistoryConfiguration : IEntityTypeConfiguration<ChangeRequestHistory>
{
    public void Configure(EntityTypeBuilder<ChangeRequestHistory> builder)
    {
        // Append-only: a trigger in the InitialSchema migration blocks UPDATE and DELETE.
        builder.ToTable("change_request_history");
        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.OccurredAt).HasDefaultValueSql("now()").ValueGeneratedNever();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);

        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(x => x.ChangeRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ChangeRequestId, x.OccurredAt });
    }
}
