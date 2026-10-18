using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("change_requests", t =>
        {
            t.HasCheckConstraint("ck_change_requests_type", SqlCheck.In("type", ChangeRequestType.All));
            t.HasCheckConstraint("ck_change_requests_status", SqlCheck.In("status", ChangeRequestStatus.All));
            t.HasCheckConstraint("ck_change_requests_no_self_decision",
                "decided_by_id IS NULL OR decided_by_id <> requested_by_id");
            t.HasCheckConstraint("ck_change_requests_decision_complete",
                "status NOT IN ('approved', 'rejected', 'returned') OR " +
                "(decided_by_id IS NOT NULL AND decided_at IS NOT NULL AND length(btrim(decision_reason)) > 0)");
        });

        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Type).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CostImpact).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        AuditColumns.Configure(builder);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => x.RequestedById);
    }
}
