using Pmo.Domain.Delivery;

namespace DataGenerator.Generation;

/// <summary>27 risks and issues, written out by hand so the high-exposure cases are obvious (score = probability x impact).</summary>
public static class RiskSeeder
{
    private record RiskPlan(string Project, string Type, string Title, int Probability, int Impact, string Status);

    private static readonly RiskPlan[] Plans =
    [
        new("PRJ-001", RiskType.Risk, "Key developer leaves before go-live", 4, 4, RiskStatus.Open),                    // 16 open high
        new("PRJ-001", RiskType.Risk, "App store review delays release", 2, 3, RiskStatus.Open),
        new("PRJ-001", RiskType.Issue, "Legacy API rate limits hit in testing", 3, 3, RiskStatus.Mitigating),
        new("PRJ-002", RiskType.Risk, "Vendor monitoring agent not certified", 4, 5, RiskStatus.Mitigating),             // 20 open high
        new("PRJ-002", RiskType.Risk, "Data centre access windows are short", 3, 2, RiskStatus.Open),
        new("PRJ-002", RiskType.Issue, "Alert noise above agreed threshold", 3, 4, RiskStatus.Open),
        new("PRJ-003", RiskType.Risk, "Branch routers delivered late", 3, 4, RiskStatus.Open),
        new("PRJ-003", RiskType.Risk, "Pilot sites change opening hours", 2, 2, RiskStatus.Closed),
        new("PRJ-003", RiskType.Issue, "Carrier circuit order rejected", 2, 5, RiskStatus.Mitigating),
        new("PRJ-004", RiskType.Risk, "Legacy billing data quality", 3, 4, RiskStatus.Closed),
        new("PRJ-004", RiskType.Issue, "Reconciliation gap in final cutover", 2, 3, RiskStatus.Closed),
        new("PRJ-005", RiskType.Risk, "Partner ordering API changes", 3, 3, RiskStatus.Open),
        new("PRJ-005", RiskType.Risk, "Peak-season load not tested", 2, 4, RiskStatus.Open),
        new("PRJ-006", RiskType.Risk, "Funding freeze extends beyond Q4", 4, 5, RiskStatus.Closed),                      // 20 closed high: must not count
        new("PRJ-006", RiskType.Risk, "Source system owners unavailable", 3, 3, RiskStatus.Open),
        new("PRJ-006", RiskType.Issue, "Storage cost above estimate", 2, 3, RiskStatus.Mitigating),
        new("PRJ-007", RiskType.Risk, "Field devices vary by region", 2, 3, RiskStatus.Open),
        new("PRJ-007", RiskType.Risk, "Offline sync conflicts", 3, 3, RiskStatus.Mitigating),
        new("PRJ-007", RiskType.Issue, "Beta testers report battery drain", 2, 2, RiskStatus.Open),
        new("PRJ-008", RiskType.Risk, "CRM vendor upgrade breaks connectors", 3, 5, RiskStatus.Open),                    // 15 open high
        new("PRJ-008", RiskType.Risk, "Scope creep from sales teams", 4, 3, RiskStatus.Mitigating),
        new("PRJ-008", RiskType.Issue, "Duplicate customer records in staging", 3, 3, RiskStatus.Open),
        new("PRJ-009", RiskType.Issue, "Chatbot accuracy below target", 5, 5, RiskStatus.Open),                           // 25 open high
        new("PRJ-009", RiskType.Risk, "Language model licence cost rises", 4, 3, RiskStatus.Mitigating),
        new("PRJ-009", RiskType.Risk, "Privacy review finds gaps", 2, 4, RiskStatus.Open),
        new("PRJ-010", RiskType.Risk, "Fibre survey data incomplete", 3, 3, RiskStatus.Open),
        new("PRJ-010", RiskType.Risk, "Project not yet funded in full", 2, 4, RiskStatus.Open)
    ];

    public static void Seed(SeedData data, Rng rng)
    {
        var id = 1;
        foreach (var plan in Plans)
        {
            var project = data.ProjectByCode(plan.Project);
            var people = ProjectSeeder.ContributorsOf(data, project.Id).Append(project.OwnerId).ToList();
            var closed = plan.Status == RiskStatus.Closed;
            var score = plan.Probability * plan.Impact;

            data.Risks.Add(new Risk
            {
                Id = id++, ProjectId = project.Id, Type = plan.Type, Title = plan.Title,
                Description = $"{plan.Title}. Raised during delivery review of {project.Name}.",
                OwnerId = rng.Pick(people),
                Probability = (short)plan.Probability, Impact = (short)plan.Impact,
                Mitigation = score >= 15 || rng.Chance(0.5) ? $"Mitigation plan agreed for: {plan.Title.ToLowerInvariant()}." : null,
                DueDate = SeedClock.AsOf.AddDays(rng.Between(-30, 90)),
                Status = plan.Status,
                ClosedAt = closed ? SeedClock.Utc(SeedClock.AsOf.AddDays(-rng.Between(10, 60)), 11) : null,
                CreatedAt = SeedClock.Utc(SeedClock.Min(project.StartDate.AddDays(rng.Between(20, 120)), SeedClock.AsOf)), CreatedById = project.OwnerId
            });
        }
    }
}
