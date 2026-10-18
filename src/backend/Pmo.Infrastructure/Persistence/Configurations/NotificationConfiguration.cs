using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Automation;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.LinkUrl).HasMaxLength(300);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OutboxMessage>().WithMany().HasForeignKey(x => x.SourceOutboxId).OnDelete(DeleteBehavior.Restrict);

        // A second delivery of the same outbox message to the same user is impossible.
        builder.HasIndex(x => new { x.SourceOutboxId, x.UserId }).IsUnique();
    }
}
