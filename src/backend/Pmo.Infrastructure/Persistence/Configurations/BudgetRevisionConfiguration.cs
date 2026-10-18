using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class BudgetRevisionConfiguration : IEntityTypeConfiguration<BudgetRevision>
{
    public void Configure(EntityTypeBuilder<BudgetRevision> builder)
    {
        builder.ToTable("budget_revisions", t =>
        {
            t.HasCheckConstraint("ck_budget_revisions_amount", "amount <> 0");
            t.HasCheckConstraint("ck_budget_revisions_status", SqlCheck.In("status", RevisionStatus.All));
            t.HasCheckConstraint("ck_budget_revisions_no_self_decision",
                "decided_by_id IS NULL OR decided_by_id <> requested_by_id");
            t.HasCheckConstraint("ck_budget_revisions_pending_undecided",
                "(status = 'pending') = (decided_by_id IS NULL)");
        });

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Reason).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(x => x.ChangeRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}
