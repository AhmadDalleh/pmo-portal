using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pmo.Domain.Automation;
using Pmo.Domain.Delivery;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;
using Pmo.Infrastructure.Identity;
using Pmo.Infrastructure.Persistence.Configurations;

namespace Pmo.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context. Inherits the Identity tables (users, roles, ...) and adds the business tables.
/// Table/column names become snake_case via UseSnakeCaseNamingConvention (see DependencyInjection).
/// </summary>
public class PmoDbContext : IdentityDbContext<AppUser, AppRole, int>
{
    public PmoDbContext(DbContextOptions<PmoDbContext> options) : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<ProjectTask> Tasks => Set<ProjectTask>();
    public DbSet<ProgressSnapshot> ProgressSnapshots => Set<ProgressSnapshot>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeRequestHistory> ChangeRequestHistory => Set<ChangeRequestHistory>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<CostCategory> CostCategories => Set<CostCategory>();
    public DbSet<ProjectBudget> ProjectBudgets => Set<ProjectBudget>();
    public DbSet<BudgetRevision> BudgetRevisions => Set<BudgetRevision>();
    public DbSet<CostTransaction> CostTransactions => Set<CostTransaction>();
    public DbSet<FinancialForecast> FinancialForecasts => Set<FinancialForecast>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ReportingSetting> ReportingSettings => Set<ReportingSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        RenameIdentityTables(builder);

        // Listed explicitly (no assembly scanning) so every mapping is easy to find.
        builder.ApplyConfiguration(new AppUserConfiguration());
        builder.ApplyConfiguration(new DepartmentConfiguration());
        builder.ApplyConfiguration(new ProjectConfiguration());
        builder.ApplyConfiguration(new ProjectMemberConfiguration());
        builder.ApplyConfiguration(new MilestoneConfiguration());
        builder.ApplyConfiguration(new ProjectTaskConfiguration());
        builder.ApplyConfiguration(new ProgressSnapshotConfiguration());
        builder.ApplyConfiguration(new RiskConfiguration());
        builder.ApplyConfiguration(new ChangeRequestConfiguration());
        builder.ApplyConfiguration(new ChangeRequestHistoryConfiguration());
        builder.ApplyConfiguration(new AuditEventConfiguration());
        builder.ApplyConfiguration(new CostCategoryConfiguration());
        builder.ApplyConfiguration(new ProjectBudgetConfiguration());
        builder.ApplyConfiguration(new BudgetRevisionConfiguration());
        builder.ApplyConfiguration(new CostTransactionConfiguration());
        builder.ApplyConfiguration(new FinancialForecastConfiguration());
        builder.ApplyConfiguration(new OutboxMessageConfiguration());
        builder.ApplyConfiguration(new AutomationRunConfiguration());
        builder.ApplyConfiguration(new NotificationConfiguration());
        builder.ApplyConfiguration(new ReportingSettingConfiguration());
    }

    /// <summary>Identity defaults to AspNetUsers etc.; the design doc names them users, roles, ...</summary>
    private static void RenameIdentityTables(ModelBuilder builder)
    {
        builder.Entity<AppRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<int>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<int>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("role_claims");
    }
}
