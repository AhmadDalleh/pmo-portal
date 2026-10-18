using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Finance;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class CostCategoryConfiguration : IEntityTypeConfiguration<CostCategory>
{
    public void Configure(EntityTypeBuilder<CostCategory> builder)
    {
        builder.ToTable("cost_categories");
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
