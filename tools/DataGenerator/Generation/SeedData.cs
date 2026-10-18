using Pmo.Domain.Automation;
using Pmo.Domain.Delivery;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;
using Pmo.Domain.Portfolio;

namespace DataGenerator.Generation;

/// <summary>Everything the generator produces, as plain domain objects. The CSV exporter and the edge-case checks both read this.</summary>
public class SeedData
{
    public List<Department> Departments { get; } = [];
    public List<SeedUser> Users { get; } = [];
    public List<CostCategory> Categories { get; } = [];
    public List<Project> Projects { get; } = [];
    public List<ProjectMember> Members { get; } = [];
    public List<ProjectBudget> Budgets { get; } = [];
    public List<Milestone> Milestones { get; } = [];
    public List<ProjectTask> Tasks { get; } = [];
    public List<ProgressSnapshot> Snapshots { get; } = [];
    public List<Risk> Risks { get; } = [];
    public List<ChangeRequest> ChangeRequests { get; } = [];
    public List<ChangeRequestHistory> History { get; } = [];
    public List<AuditEvent> AuditEvents { get; } = [];
    public List<BudgetRevision> Revisions { get; } = [];
    public List<CostTransaction> Costs { get; } = [];
    public List<FinancialForecast> Forecasts { get; } = [];
    public List<OutboxMessage> Outbox { get; } = [];
    public List<AutomationRun> Runs { get; } = [];
    public List<Notification> Notifications { get; } = [];

    public Project ProjectByCode(string code) => Projects.Single(p => p.Code == code);

    public SeedUser UserByEmail(string email) => Users.Single(u => u.Email == email);
}
