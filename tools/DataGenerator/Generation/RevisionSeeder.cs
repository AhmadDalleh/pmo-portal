using Pmo.Domain.Finance;
using Pmo.Domain.Governance;

namespace DataGenerator.Generation;

/// <summary>8 budget revisions: approved, rejected, pending, a reduction, one dated in the future, and three linked to approved CRs.</summary>
public static class RevisionSeeder
{
    private record RevisionPlan(string Project, decimal Amount, DateOnly Effective, string Status, bool LinkCr, string Reason);

    private static readonly RevisionPlan[] Plans =
    [
        new("PRJ-001", 100_000m, new(2026, 3, 1), RevisionStatus.Approved, true, "Accessibility remediation funded."),
        new("PRJ-002", 300_000m, new(2026, 5, 1), RevisionStatus.Approved, true, "Extra probes for remote sites."),
        new("PRJ-005", -30_000m, new(2026, 7, 1), RevisionStatus.Approved, false, "Return unused contingency."),
        new("PRJ-007", -50_000m, new(2026, 8, 1), RevisionStatus.Rejected, false, "Proposed saving on device purchase."),
        new("PRJ-007", 100_000m, new(2026, 11, 1), RevisionStatus.Approved, false, "Q4 field rollout extension (starts after the cutoff)."),
        new("PRJ-008", 150_000m, new(2026, 6, 1), RevisionStatus.Approved, true, "CRM sync module."),
        new("PRJ-008", 100_000m, new(2026, 10, 15), RevisionStatus.Pending, false, "Connector hardening, awaiting approval."),
        new("PRJ-009", 50_000m, new(2026, 8, 1), RevisionStatus.Approved, true, "Additional language support.")
    ];

    public static void Seed(SeedData data)
    {
        var admin = PeopleSeeder.AdminId(data, 2);
        var id = 1;
        foreach (var plan in Plans)
        {
            var project = data.ProjectByCode(plan.Project);
            var cr = plan.LinkCr
                ? data.ChangeRequests.First(c => c.ProjectId == project.Id && c.Type == ChangeRequestType.Budget
                                                 && c.Status == ChangeRequestStatus.Approved)
                : null;
            var requestedAt = cr is null
                ? SeedClock.Utc(plan.Status == RevisionStatus.Pending ? new DateOnly(2026, 9, 20) : plan.Effective.AddDays(-30))
                : cr.DecidedAt!.Value.AddDays(1);
            var decided = plan.Status != RevisionStatus.Pending;

            data.Revisions.Add(new BudgetRevision
            {
                Id = id++, ProjectId = project.Id, Amount = plan.Amount, Reason = plan.Reason, EffectiveDate = plan.Effective,
                Status = plan.Status, ChangeRequestId = cr?.Id, RequestedById = project.OwnerId, RequestedAt = requestedAt,
                DecidedById = decided ? admin : null, DecidedAt = decided ? requestedAt.AddDays(2) : null,
                DecisionReason = decided ? (plan.Status == RevisionStatus.Approved ? "Approved." : "Rejected: not needed now.") : null
            });
        }
    }
}
