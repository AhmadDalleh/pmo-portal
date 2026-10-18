using System.Text.Json;
using Pmo.Domain.Governance;

namespace DataGenerator.Generation;

/// <summary>
/// 18 change requests with their full history (design doc §3.3). Each plan lists the statuses a request
/// went through after "draft"; the history and audit rows are produced from that path, so they always match
/// the final state. Decisions are never made by the requester.
/// </summary>
public static class GovernanceSeeder
{
    private record CrPlan(string Project, string Type, string Title, DateOnly Created, string[] Path,
        decimal? CostImpact, int? Days, bool ByOwner = false);

    private static readonly CrPlan[] Plans =
    [
        new("PRJ-008", ChangeRequestType.Budget, "Add CRM sync module", new(2026, 1, 12), ["submitted", "approved"], 150_000m, 20),
        new("PRJ-002", ChangeRequestType.Budget, "Extra probes for remote sites", new(2026, 2, 2), ["submitted", "returned", "submitted", "approved"], 300_000m, 30, true),
        new("PRJ-001", ChangeRequestType.Budget, "Accessibility remediation", new(2026, 2, 16), ["submitted", "approved"], 100_000m, 10),
        new("PRJ-009", ChangeRequestType.Budget, "Additional language support", new(2026, 4, 14), ["submitted", "approved"], 50_000m, 5),
        new("PRJ-005", ChangeRequestType.Scope, "Add punch-out catalogue", new(2026, 3, 2), ["submitted", "approved"], null, 14),
        new("PRJ-001", ChangeRequestType.Scope, "Add in-app chat", new(2026, 3, 20), ["submitted", "rejected"], 80_000m, 25),
        new("PRJ-003", ChangeRequestType.Schedule, "Move pilot start by two weeks", new(2026, 4, 20), ["submitted", "returned"], null, 14),
        new("PRJ-002", ChangeRequestType.Resource, "Second network engineer for six months", new(2026, 5, 4), ["submitted", "rejected"], 120_000m, null),
        new("PRJ-006", ChangeRequestType.Schedule, "Pause ingestion workstream", new(2026, 5, 18), ["withdrawn"], null, 30),
        new("PRJ-004", ChangeRequestType.Scope, "Archive historic invoices", new(2026, 5, 25), ["submitted", "returned", "withdrawn"], 20_000m, 7),
        new("PRJ-008", ChangeRequestType.Scope, "Real-time sync for contacts", new(2026, 6, 8), ["submitted", "returned"], 60_000m, 15),
        new("PRJ-003", ChangeRequestType.Scope, "Add second pilot city", new(2026, 9, 14), ["submitted"], 90_000m, 21),
        new("PRJ-009", ChangeRequestType.Schedule, "Extend UAT by one week", new(2026, 9, 18), ["submitted"], null, 7),
        new("PRJ-001", ChangeRequestType.Resource, "Add UX designer", new(2026, 9, 21), ["submitted"], 45_000m, null),
        new("PRJ-008", ChangeRequestType.Resource, "Add integration tester", new(2026, 9, 23), ["submitted"], 35_000m, null),
        new("PRJ-005", ChangeRequestType.Budget, "Fund load-testing tools", new(2026, 9, 25), [], 40_000m, null),
        new("PRJ-006", ChangeRequestType.Resource, "Contract data steward", new(2026, 9, 26), [], 55_000m, null),
        new("PRJ-007", ChangeRequestType.Schedule, "Release in two waves", new(2026, 9, 24), ["submitted", "approved"], null, 10)
    ];

    public static void Seed(SeedData data, Rng rng)
    {
        var admin1 = PeopleSeeder.AdminId(data, 1);
        var admin2 = PeopleSeeder.AdminId(data, 2);
        var history = new List<ChangeRequestHistory>();
        var id = 1;

        foreach (var plan in Plans)
        {
            var project = data.ProjectByCode(plan.Project);
            var requester = plan.ByOwner ? project.OwnerId : rng.Pick(ProjectSeeder.ContributorsOf(data, project.Id));
            var decider = requester == project.OwnerId ? admin1 : id % 2 == 0 ? project.OwnerId : admin2;
            var created = SeedClock.Utc(plan.Created);

            var cr = new ChangeRequest
            {
                Id = id, Code = $"CR-{id:D4}", ProjectId = project.Id, Title = plan.Title,
                Description = $"{plan.Title}. Requested for {project.Name}; see attached impact estimate.",
                Type = plan.Type, CostImpact = plan.CostImpact, ScheduleImpactDays = plan.Days,
                Status = ChangeRequestStatus.Draft, RequestedById = requester, CreatedAt = created, CreatedById = requester
            };
            data.ChangeRequests.Add(cr);
            history.Add(Row(id, null, ChangeRequestStatus.Draft, requester, null, created));

            var from = ChangeRequestStatus.Draft;
            var at = created;
            foreach (var to in plan.Path)
            {
                at = at.AddDays(3).AddHours(1);
                var isDecision = to is ChangeRequestStatus.Approved or ChangeRequestStatus.Rejected or ChangeRequestStatus.Returned;
                var actor = isDecision ? decider : requester;
                var reason = isDecision ? ReasonFor(to) : null;

                if (to == ChangeRequestStatus.Submitted)
                {
                    cr.SubmittedAt = at;
                    (cr.DecidedById, cr.DecidedAt, cr.DecisionReason) = (null, null, null);   // resubmission clears the decision
                }
                else if (isDecision)
                {
                    (cr.DecidedById, cr.DecidedAt, cr.DecisionReason) = (decider, at, reason);
                }

                cr.Status = to;
                history.Add(Row(id, from, to, actor, reason, at));
                from = to;
            }

            cr.UpdatedAt = at;
            cr.UpdatedById = requester;
            id++;
        }

        // History and audit ids follow time order, like they would in a real system.
        var ordered = history.OrderBy(h => h.OccurredAt).ThenBy(h => h.ChangeRequestId).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Id = i + 1;
            data.History.Add(ordered[i]);
            data.AuditEvents.Add(AuditFor(data, ordered[i]));
        }
    }

    private static ChangeRequestHistory Row(int crId, string? from, string to, int actor, string? reason, DateTimeOffset at) =>
        new() { ChangeRequestId = crId, FromStatus = from, ToStatus = to, ActorId = actor, Reason = reason, OccurredAt = at, CorrelationId = $"seed-cr-{crId}" };

    private static string ReasonFor(string decision) => decision switch
    {
        ChangeRequestStatus.Approved => "Approved: benefit is clear and the impact is acceptable.",
        ChangeRequestStatus.Rejected => "Rejected: not justified at this stage.",
        _ => "Returned: please add effort estimate and update the impact."
    };

    /// <summary>The same action names the API writes: created, submitted, withdrawn, decided.</summary>
    private static string ActionFor(ChangeRequestHistory row) => row.ToStatus switch
    {
        ChangeRequestStatus.Draft => "change_request.created",
        ChangeRequestStatus.Submitted => "change_request.submitted",
        ChangeRequestStatus.Withdrawn => "change_request.withdrawn",
        _ => "change_request.decided"
    };

    private static AuditEvent AuditFor(SeedData data, ChangeRequestHistory row)
    {
        var projectId = data.ChangeRequests.Single(c => c.Id == row.ChangeRequestId).ProjectId;
        return new AuditEvent
        {
            OccurredAt = row.OccurredAt, ActorId = row.ActorId, ProjectId = projectId, EntityType = "ChangeRequest",
            EntityId = row.ChangeRequestId.ToString(), Action = ActionFor(row), CorrelationId = row.CorrelationId,
            Data = JsonSerializer.Serialize(new { from = row.FromStatus, to = row.ToStatus, reason = row.Reason })
        };
    }
}
