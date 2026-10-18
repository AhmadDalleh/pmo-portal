using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Automation;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ReportingSettingConfiguration : IEntityTypeConfiguration<ReportingSetting>
{
    private static readonly DateOnly DefaultAsOf = new(2026, 9, 30);

    public void Configure(EntityTypeBuilder<ReportingSetting> builder)
    {
        builder.ToTable("reporting_settings", t =>
            t.HasCheckConstraint("ck_reporting_settings_single_row", "id = 1"));

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.AsOfDate).HasDefaultValue(DefaultAsOf).ValueGeneratedNever();

        // The single row exists from the first migration.
        builder.HasData(new ReportingSetting { Id = 1, AsOfDate = DefaultAsOf });
    }
}
