using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Finance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class CostTransactionConfiguration : IEntityTypeConfiguration<CostTransaction>
{
    public void Configure(EntityTypeBuilder<CostTransaction> builder)
    {
        // Posted rows are immutable: trigger trg_cost_transactions_posted_immutable (InitialSchema migration).
        builder.ToTable("cost_transactions", t =>
        {
            t.HasCheckConstraint("ck_cost_transactions_status", SqlCheck.In("status", CostStatus.All));
            t.HasCheckConstraint("ck_cost_transactions_amount_sign",
                "(reverses_transaction_id IS NULL AND amount > 0) OR " +
                "(reverses_transaction_id IS NOT NULL AND amount < 0)");
            t.HasCheckConstraint("ck_cost_transactions_posted_fields",
                "(status = 'posted') = (posted_by_id IS NOT NULL AND posted_at IS NOT NULL)");
        });

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(300).IsRequired();
        builder.Property(x => x.VendorName).HasMaxLength(150);
        builder.Property(x => x.ReferenceNo).HasMaxLength(50);
        builder.Property(x => x.Status).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Version).IsRowVersion();

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostCategory>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CostTransaction>().WithMany().HasForeignKey(x => x.ReversesTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.PostedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ReversesTransactionId).IsUnique();   // one reversal per transaction
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();          // no double create
        builder.HasIndex(x => x.CategoryId);

        // Covering index for actual spend and the monthly trend: the query never touches the table.
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.TxnDate })
            .IncludeProperties(x => new { x.Amount, x.CategoryId });
    }
}
