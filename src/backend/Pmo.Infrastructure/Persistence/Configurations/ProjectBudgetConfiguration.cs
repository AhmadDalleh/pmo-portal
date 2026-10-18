using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Finance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ProjectBudgetConfiguration : IEntityTypeConfiguration<ProjectBudget>
{
    public void Configure(EntityTypeBuilder<ProjectBudget> builder)
    {
        builder.ToTable("project_budgets", t =>
        {
            t.HasCheckConstraint("ck_project_budgets_initial_amount", "initial_amount >= 0");
            t.HasCheckConstraint("ck_project_budgets_currency", "currency = 'AED'");
        });

        builder.Property(x => x.InitialAmount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasColumnType("char(3)").HasDefaultValue("AED").ValueGeneratedNever();

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict);

        // One budget per project.
        builder.HasIndex(x => x.ProjectId).IsUnique();
    }
}
