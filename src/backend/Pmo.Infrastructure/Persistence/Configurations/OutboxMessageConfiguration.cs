using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Automation;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", t =>
            t.HasCheckConstraint("ck_outbox_messages_status", SqlCheck.In("status", OutboxStatus.All)));

        builder.Property(x => x.Type).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DedupKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Attempts).HasDefaultValue(0).ValueGeneratedNever();

        builder.HasIndex(x => x.DedupKey).IsUnique();

        // Worker polling: only pending rows are indexed, so the index stays tiny.
        builder.HasIndex(x => x.AvailableAt)
            .HasDatabaseName("ix_outbox_messages_available_at_pending")
            .HasFilter("status = 'pending'");
    }
}
