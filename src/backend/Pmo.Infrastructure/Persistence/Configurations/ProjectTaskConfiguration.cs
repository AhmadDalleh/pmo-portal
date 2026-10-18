using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Delivery;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTask>
{
    public void Configure(EntityTypeBuilder<ProjectTask> builder)
    {
        builder.ToTable("tasks", t =>
        {
            t.HasCheckConstraint("ck_tasks_status", SqlCheck.In("status", ProjectTaskStatus.All));
            t.HasCheckConstraint("ck_tasks_priority", SqlCheck.In("priority", ProjectTaskPriority.All));
            t.HasCheckConstraint("ck_tasks_dates", "start_date IS NULL OR due_date >= start_date");
            t.HasCheckConstraint("ck_tasks_weight", "weight BETWEEN 1 AND 10");
            t.HasCheckConstraint("ck_tasks_progress_pct", "progress_pct BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_tasks_done_is_100", "status <> 'done' OR progress_pct = 100");
        });

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Priority).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Weight).HasDefaultValue((short)1).ValueGeneratedNever();
        builder.Property(x => x.ProgressPct).HasDefaultValue((short)0).ValueGeneratedNever();
        AuditColumns.Configure(builder);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);

        // Composite FK (milestone_id, project_id) -> milestones (id, project_id):
        // a task can only point at a milestone of its own project. NULL milestone_id skips the check.
        builder.HasOne<Milestone>().WithMany()
            .HasForeignKey(x => new { x.MilestoneId, x.ProjectId })
            .HasPrincipalKey(m => new { m.Id, m.ProjectId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProjectId, x.Status, x.DueDate });
        builder.HasIndex(x => new { x.AssigneeId, x.Status });
        builder.HasIndex(x => x.DueDate)
            .HasDatabaseName("ix_tasks_due_date_open")
            .HasFilter("status NOT IN ('done', 'cancelled')");
    }
}
