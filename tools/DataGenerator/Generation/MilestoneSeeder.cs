using Pmo.Domain.Delivery;

namespace DataGenerator.Generation;

/// <summary>2-4 milestones per project, evenly spread over the project, plus a few deliberate edge cases.</summary>
public static class MilestoneSeeder
{
    private static readonly string[] Names =
        ["Requirements signed off", "Design complete", "Build complete", "User acceptance passed", "Go-live"];

    // (project code, milestone index) -> forced due date; always pending.
    private static readonly Dictionary<(string, int), DateOnly> ForcedPending = new()
    {
        [("PRJ-001", 1)] = new DateOnly(2026, 10, 10),   // due in 10 days
        [("PRJ-007", 1)] = new DateOnly(2026, 10, 14),   // exactly asOf + 14: the boundary of "due within 14 days"
        [("PRJ-006", 0)] = new DateOnly(2026, 8, 31),    // overdue milestone
        [("PRJ-005", 0)] = new DateOnly(2026, 9, 15)     // overdue milestone
    };

    public static void Seed(SeedData data, Rng rng)
    {
        var id = 1;
        foreach (var plan in ProjectPlan.All)
        {
            var project = data.ProjectByCode(plan.Code);
            var span = plan.PlannedEnd.DayNumber - plan.Start.DayNumber;
            for (var k = 0; k < plan.MilestoneCount; k++)
            {
                var due = plan.Start.AddDays(span * (k + 1) / (plan.MilestoneCount + 1));
                var forced = ForcedPending.TryGetValue((plan.Code, k), out var forcedDue);
                if (forced)
                {
                    due = forcedDue;
                }

                var done = !forced && (plan.Status == "completed" || (due < SeedClock.AsOf && plan.Status != "on_hold"));
                data.Milestones.Add(new Milestone
                {
                    Id = id++, ProjectId = project.Id, Name = Names[k % Names.Length],
                    Description = $"{Names[k % Names.Length]} for {plan.Name}.",
                    DueDate = due, Status = done ? MilestoneStatus.Completed : MilestoneStatus.Pending,
                    CompletedAt = done ? SeedClock.Max(plan.Start, due.AddDays(-rng.Between(0, 5))) : null,
                    CreatedAt = project.CreatedAt, CreatedById = project.OwnerId
                });
            }
        }
    }
}
