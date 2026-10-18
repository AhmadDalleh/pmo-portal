using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;

namespace Pmo.Infrastructure.Persistence.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("project_members", t =>
            t.HasCheckConstraint("ck_project_members_role", SqlCheck.In("member_role", MemberRole.All)));

        builder.HasKey(x => new { x.ProjectId, x.UserId });
        builder.Property(x => x.MemberRole).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AddedAt).HasDefaultValueSql("now()").ValueGeneratedNever();

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

        // The PK covers lookups by project_id; this one serves "which projects is this user in".
        builder.HasIndex(x => x.UserId);
    }
}
