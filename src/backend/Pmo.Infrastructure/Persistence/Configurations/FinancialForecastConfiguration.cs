using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Finance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class FinancialForecastConfiguration : IEntityTypeConfiguration<FinancialForecast>
{
    public void Configure(EntityTypeBuilder<FinancialForecast> builder)
    {
        builder.ToTable("financial_forecasts", t =>
        {
            t.HasCheckConstraint("ck_financial_forecasts_first_of_month", "EXTRACT(day FROM period_month) = 1");
            t.HasCheckConstraint("ck_financial_forecasts_amount", "amount >= 0");
        });

        builder.Property(x => x.Amount).HasPrecision(18, 2);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProjectId, x.CategoryId, x.PeriodMonth }).IsUnique();
    }
}
