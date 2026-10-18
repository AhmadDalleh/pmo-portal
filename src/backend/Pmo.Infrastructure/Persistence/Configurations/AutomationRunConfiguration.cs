using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Automation;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class AutomationRunConfiguration : IEntityTypeConfiguration<AutomationRun>
{
    public void Configure(EntityTypeBuilder<AutomationRun> builder)
    {
        builder.ToTable("automation_runs", t =>
            t.HasCheckConstraint("ck_automation_runs_status", SqlCheck.In("status", RunStatus.All)));

        builder.Property(x => x.JobName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
    }
}
