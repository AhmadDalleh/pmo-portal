using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Delivery;
using Pmo.Domain.Portfolio;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> builder)
    {
        builder.ToTable("milestones", t =>
        {
            t.HasCheckConstraint("ck_milestones_status", SqlCheck.In("status", MilestoneStatus.All));
            t.HasCheckConstraint("ck_milestones_completed_at",
                "(status = 'completed') = (completed_at IS NOT NULL)");
        });

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        AuditColumns.Configure(builder);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);

        // UNIQUE (id, project_id): the target of the tasks composite FK.
        builder.HasAlternateKey(x => new { x.Id, x.ProjectId });

        builder.HasIndex(x => new { x.ProjectId, x.DueDate });
    }
}
