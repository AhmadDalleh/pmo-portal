using Pmo.Domain.Delivery;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;

namespace DataGenerator.Generation;

/// <summary>
/// Design doc §10 as code. Every line of the mandatory checklist and every volume target is tested here;
/// if one is missing the generator throws and writes nothing (story I2).
/// </summary>
public static class EdgeCaseChecks
{
    public static void AssertAll(SeedData d)
    {
        var asOf = SeedClock.AsOf;
        var failures = new List<string>();

        void Check(bool ok, string rule)
        {
            if (!ok)
            {
                failures.Add(rule);
            }
        }

        // Volumes
        Check(d.Users.Count == 18 && d.Users.Count(u => u.Role == PeopleSeeder.Admin) == 2
              && d.Users.Count(u => u.Role == PeopleSeeder.ProjectManager) == 5, "18 users: 2 admins, 5 PMs, 11 contributors");
        Check(d.Departments.Count == 3, "3 departments");
        Check(d.Projects.Count == 10, "10 projects");
        Check(d.Tasks.Count >= 100, ">= 100 tasks");
        Check(d.Milestones.Count >= 30, ">= 30 milestones");
        Check(d.Risks.Count >= 24, ">= 24 risks");
        Check(d.ChangeRequests.Count >= 16 && d.History.Count > d.ChangeRequests.Count, ">= 16 change requests with history");
        Check(d.Categories.Count == 5, "5 cost categories");
        Check(d.Costs.Count >= 100, ">= 100 cost transactions");
        Check(d.Revisions.Count >= 6, ">= 6 budget revisions");
        Check(d.Snapshots.Count >= 10, "progress snapshots");
        Check(d.Outbox.Count + d.Runs.Count >= 20, ">= 20 outbox rows and automation runs");

        // Checklist
        Check(d.Tasks.Count(t => t.DueDate < asOf && t.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled)) >= 5, ">= 5 overdue incomplete tasks");
        Check(d.Tasks.Any(t => t.DueDate == asOf && t.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled)), "a task due exactly on asOf");
        Check(d.Tasks.Any(t => t.AssigneeId is null), "an unassigned task");
        Check(d.Tasks.Any(t => t.Status == ProjectTaskStatus.Blocked), "a blocked task");
        Check(d.Tasks.Any(t => t.Status == ProjectTaskStatus.Cancelled), "a cancelled task");
        Check(d.Projects.Any(p => p.Status == "completed" && p.ActualEndDate is not null), "a completed project with actual_end_date");
        Check(d.Risks.Any(r => r.Status == RiskStatus.Closed && r.Probability * r.Impact >= 15), "a closed high-exposure risk");
        Check(d.Risks.Any(r => r.Status != RiskStatus.Closed && r.Probability * r.Impact >= 15), "an open high-exposure risk");
        Check(d.Milestones.Any(m => m.Status == MilestoneStatus.Pending && m.DueDate < asOf), "an overdue milestone");
        Check(d.Milestones.Any(m => m.Status == MilestoneStatus.Pending && m.DueDate >= asOf && m.DueDate <= asOf.AddDays(14)), "a milestone due within 14 days");
        Check(d.ChangeRequests.Any(c => c.Status == ChangeRequestStatus.Returned), "a returned change request");
        Check(d.ChangeRequests.Any(c => c.Status == ChangeRequestStatus.Rejected), "a rejected change request");
        Check(d.ChangeRequests.Any(c => c.Status == ChangeRequestStatus.Submitted), "a pending (submitted) change request");
        Check(d.Revisions.Any(r => r.Status == RevisionStatus.Approved) && d.Revisions.Any(r => r.Status == RevisionStatus.Pending), "an approved and a pending revision");
        Check(d.Costs.Any(c => c.Status == CostStatus.Posted) && d.Costs.Any(c => c.Status == CostStatus.Draft), "posted and draft costs");
        Check(d.Costs.Any(c => c.TxnDate > asOf), "a cost dated after asOf");
        Check(d.Costs.Any(c => c.ReversesTransactionId is not null), "a reversal");
        Check(d.Projects.Select(p => p.DepartmentId).Distinct().Count() == 3, "projects in all 3 departments");
        Check(d.Costs.Select(c => (c.TxnDate.Year, c.TxnDate.Month)).Distinct().Count() >= 12, "costs across >= 12 months");

        // The edge case each project carries
        CheckFinance(d, failures);

        if (failures.Count > 0)
        {
            throw new InvalidOperationException("Seed data is missing required edge cases:\n - " + string.Join("\n - ", failures));
        }
    }

    private static void CheckFinance(SeedData d, List<string> failures)
    {
        var asOf = SeedClock.AsOf;
        decimal Budget(string code) => SeedMath.CurrentBudget(d, d.ProjectByCode(code).Id, asOf);
        decimal Spend(string code) => SeedMath.ActualSpend(d, d.ProjectByCode(code).Id, asOf);
        decimal Ftc(string code) => SeedMath.ForecastToComplete(d, d.ProjectByCode(code).Id, asOf);

        var nearBudget = Spend("PRJ-001") / Budget("PRJ-001");
        if (nearBudget is < 0.90m or > 1.00m)
        {
            failures.Add($"PRJ-001 near budget (utilisation {nearBudget:P1}, expected 90-100%)");
        }

        if (Spend("PRJ-002") + Ftc("PRJ-002") <= Budget("PRJ-002"))
        {
            failures.Add("PRJ-002 forecast overrun (EAC must exceed current budget)");
        }

        if (d.Costs.Any(c => c.ProjectId == d.ProjectByCode("PRJ-003").Id && c.Status == CostStatus.Posted)
            || !d.Costs.Any(c => c.ProjectId == d.ProjectByCode("PRJ-003").Id))
        {
            failures.Add("PRJ-003 must have only draft costs");
        }

        if (d.Forecasts.Any(f => f.ProjectId == d.ProjectByCode("PRJ-005").Id))
        {
            failures.Add("PRJ-005 must have no forecast rows");
        }

        if (Spend("PRJ-009") <= Budget("PRJ-009"))
        {
            failures.Add("PRJ-009 must be overspent");
        }

        if (SeedMath.Progress(d, d.ProjectByCode("PRJ-010").Id) is not null)
        {
            failures.Add("PRJ-010 must have no tasks");
        }

        var chatbotId = d.ProjectByCode("PRJ-009").Id;
        if (!d.Costs.Any(c => c.ProjectId == chatbotId && c.TxnDate > asOf && c.Status == CostStatus.Posted))
        {
            failures.Add("PRJ-009 needs a posted cost dated after the cutoff");
        }

        var crmId = d.ProjectByCode("PRJ-008").Id;
        var crm = d.Revisions.Where(r => r.ProjectId == crmId).ToList();
        if (!crm.Any(r => r.Status == RevisionStatus.Approved && r.ChangeRequestId is not null)
            || !crm.Any(r => r.Status == RevisionStatus.Pending))
        {
            failures.Add("PRJ-008 needs an approved revision linked to a CR plus a pending revision");
        }
    }
}
