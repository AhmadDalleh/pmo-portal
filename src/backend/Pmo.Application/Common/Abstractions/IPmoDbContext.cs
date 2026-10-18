using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pmo.Domain.Automation;
using Pmo.Domain.Delivery;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;

namespace Pmo.Application.Common.Abstractions;

/// <summary>
/// The business tables as services see them. PmoDbContext (Infrastructure) implements it, so Application never
/// references a database provider. Identity users are reached through IUserDirectory instead.
/// </summary>
public interface IPmoDbContext
{
    DbSet<Department> Departments { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectMember> ProjectMembers { get; }
    DbSet<Milestone> Milestones { get; }
    DbSet<ProjectTask> Tasks { get; }
    DbSet<ProgressSnapshot> ProgressSnapshots { get; }
    DbSet<Risk> Risks { get; }
    DbSet<ChangeRequest> ChangeRequests { get; }
    DbSet<ChangeRequestHistory> ChangeRequestHistory { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<CostCategory> CostCategories { get; }
    DbSet<ProjectBudget> ProjectBudgets { get; }
    DbSet<BudgetRevision> BudgetRevisions { get; }
    DbSet<CostTransaction> CostTransactions { get; }
    DbSet<FinancialForecast> FinancialForecasts { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<AutomationRun> AutomationRuns { get; }
    DbSet<Notification> Notifications { get; }

    DatabaseFacade Database { get; }

    EntityEntry<T> Entry<T>(T entity) where T : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
