using Pmo.Domain.Delivery;

namespace DataGenerator.Generation;

/// <summary>
/// About 120 tasks. Status follows the due date (past due => done) so projects look realistic; the
/// stragglers, blocked, unassigned, cancelled and due-today tasks come from the tables below, so every
/// edge case is always present and easy to find.
/// </summary>
public static class TaskSeeder
{
    private static readonly string[] Titles =
    [
        "Define API contracts", "Set up CI pipeline", "Design data model", "Build login screens", "Write test plan",
        "Configure environments", "Migrate reference data", "Implement reporting module", "Security review",
        "Performance baseline", "Prepare training material", "Integrate payment gateway", "Create monitoring dashboards",
        "Document runbook", "Vendor onboarding", "User interviews", "Prototype review", "Capacity planning",
        "Implement notifications", "Data quality checks", "Release rehearsal", "Stakeholder demo", "Fix defects batch 1",
        "Fix defects batch 2", "Accessibility audit", "Backup and restore test", "Network diagram sign-off",
        "Cutover plan", "Hypercare plan", "Architecture review", "Access request workflow", "Audit log design",
        "Error handling standard", "Load test", "Deployment automation", "Operational handover"
    ];

    // (project code, task index inside the project) -> (due date, status, progress)
    private static readonly Dictionary<(string, int), (DateOnly Due, string Status, short Progress)> Forced = new()
    {
        [("PRJ-001", 3)] = (new DateOnly(2026, 9, 10), ProjectTaskStatus.InProgress, 60),   // overdue
        [("PRJ-002", 2)] = (new DateOnly(2026, 9, 5), ProjectTaskStatus.InProgress, 40),    // overdue
        [("PRJ-002", 5)] = (new DateOnly(2026, 8, 20), ProjectTaskStatus.Blocked, 20),      // overdue + blocked
        [("PRJ-002", 7)] = (new DateOnly(2026, 11, 20), ProjectTaskStatus.Blocked, 10),     // blocked, not overdue
        [("PRJ-005", 4)] = (new DateOnly(2026, 9, 25), ProjectTaskStatus.Todo, 0),          // overdue, never started
        [("PRJ-006", 4)] = (new DateOnly(2026, 8, 15), ProjectTaskStatus.Blocked, 30),      // overdue + blocked
        [("PRJ-006", 5)] = (new DateOnly(2026, 8, 25), ProjectTaskStatus.Blocked, 15),
        [("PRJ-008", 3)] = (new DateOnly(2026, 9, 1), ProjectTaskStatus.InProgress, 75),    // overdue
        [("PRJ-009", 2)] = (new DateOnly(2026, 9, 18), ProjectTaskStatus.InProgress, 35),   // overdue
        [("PRJ-007", 6)] = (new DateOnly(2026, 9, 30), ProjectTaskStatus.InProgress, 70)    // due exactly on asOf
    };

    private static readonly HashSet<(string, int)> Unassigned = [("PRJ-005", 8), ("PRJ-009", 5)];
    private static readonly HashSet<(string, int)> Cancelled = [("PRJ-002", 10), ("PRJ-004", 11)];

    public static void Seed(SeedData data, Rng rng)
    {
        var id = 1;
        foreach (var plan in ProjectPlan.All.Where(p => p.TaskCount > 0))
        {
            var project = data.ProjectByCode(plan.Code);
            var milestones = data.Milestones.Where(m => m.ProjectId == project.Id).OrderBy(m => m.DueDate).ToList();
            var people = ProjectSeeder.ContributorsOf(data, project.Id).Append(project.OwnerId).ToList();
            var titles = rng.Shuffle(Titles);
            var span = plan.PlannedEnd.DayNumber - plan.Start.DayNumber;
            var end = plan.ActualEnd ?? plan.PlannedEnd;

            for (var k = 0; k < plan.TaskCount; k++)
            {
                var due = plan.Start.AddDays(span * (k + 1) / (plan.TaskCount + 1) + rng.Between(-5, 5));
                due = SeedClock.Min(SeedClock.Max(due, plan.Start.AddDays(7)), end);
                var (status, progress) = StatusFor(plan, due, rng);

                if (Forced.TryGetValue((plan.Code, k), out var forced))
                {
                    (due, status, progress) = forced;
                }

                if (Cancelled.Contains((plan.Code, k)))
                {
                    (status, progress) = (ProjectTaskStatus.Cancelled, (short)rng.Between(0, 30));
                }

                var start = rng.Chance(0.8) ? SeedClock.Max(plan.Start, due.AddDays(-rng.Between(7, 45))) : (DateOnly?)null;
                var completedOn = SeedClock.Max(plan.Start, SeedClock.Min(due, SeedClock.AsOf).AddDays(-rng.Between(0, 6)));
                data.Tasks.Add(new ProjectTask
                {
                    Id = id++, ProjectId = project.Id,
                    MilestoneId = rng.Chance(0.85) ? milestones[k * milestones.Count / plan.TaskCount].Id : null,
                    Title = titles[k], Description = $"{titles[k]} ({plan.Code}).",
                    AssigneeId = Unassigned.Contains((plan.Code, k)) ? null : rng.Pick(people),
                    Status = status, Priority = rng.Pick(ProjectTaskPriority.All), StartDate = start, DueDate = due,
                    Weight = (short)rng.Between(1, 10), ProgressPct = progress,
                    CompletedAt = status == ProjectTaskStatus.Done ? SeedClock.Utc(completedOn, 16) : null,
                    CreatedAt = project.CreatedAt.AddDays(7), CreatedById = project.OwnerId
                });
            }
        }
    }

    private static (string Status, short Progress) StatusFor(ProjectPlan plan, DateOnly due, Rng rng)
    {
        if (plan.Status == "completed" || due < SeedClock.AsOf)
        {
            return (ProjectTaskStatus.Done, 100);
        }

        if (due < SeedClock.AsOf.AddDays(60) && rng.Chance(0.5))
        {
            return (ProjectTaskStatus.InProgress, (short)rng.Between(10, 80));
        }

        return (ProjectTaskStatus.Todo, 0);
    }
}
