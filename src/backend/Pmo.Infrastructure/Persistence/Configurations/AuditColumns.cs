using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Common;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

/// <summary>Shared mapping for the audit columns and xmin version of every AuditableEntity.</summary>
public static class AuditColumns
{
    public static void Configure<T>(EntityTypeBuilder<T> builder) where T : AuditableEntity
    {
        // DB default serves raw SQL / CSV import; EF always sends the value the service set.
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()").ValueGeneratedNever();

        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);

        // uint + IsRowVersion is mapped by Npgsql to the xmin system column (no real column is created).
        builder.Property(x => x.Version).IsRowVersion();
    }
}
