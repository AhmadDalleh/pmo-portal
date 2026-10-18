using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        // Append-only: a trigger in the InitialSchema migration blocks UPDATE and DELETE.
        builder.ToTable("audit_events");
        builder.Property(x => x.OccurredAt).HasDefaultValueSql("now()").ValueGeneratedNever();
        builder.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.Data).HasColumnType("jsonb");

        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => x.ActorId);
    }
}
