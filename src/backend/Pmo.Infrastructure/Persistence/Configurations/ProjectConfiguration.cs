using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects", t =>
        {
            t.HasCheckConstraint("ck_projects_status", SqlCheck.In("status", ProjectStatus.All));
            t.HasCheckConstraint("ck_projects_dates", "planned_end_date >= start_date");
            t.HasCheckConstraint("ck_projects_completed_has_end_date",
                "status <> 'completed' OR actual_end_date IS NOT NULL");
        });

        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        AuditColumns.Configure(builder);

        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.DepartmentId, x.Status });
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => new { x.StartDate, x.PlannedEndDate });
    }
}
