using Bogus;
using Pmo.Domain.Finance;

namespace DataGenerator.Generation;

/// <summary>
/// 120+ cost transactions. For each project the posted costs are split so that spend at the cutoff hits a chosen
/// share of the current budget (that is how PRJ-001 lands near 92% and PRJ-009 over 100%). Extra rows add the
/// drafts, a mistaken-then-reversed invoice and a cost dated after the cutoff.
/// </summary>
public static class CostSeeder
{
    // project, posted rows, spend as a share of the current budget, draft rows, has a mistaken+reversed invoice
    private record CostPlan(string Project, int Posted, decimal Utilization, int Drafts, bool Mistake);

    private static readonly CostPlan[] Plans =
    [
        new("PRJ-001", 16, 0.92m, 2, false), new("PRJ-002", 18, 0.61m, 2, true), new("PRJ-003", 0, 0m, 4, false),
        new("PRJ-004", 12, 0.82m, 0, false), new("PRJ-005", 8, 0.40m, 2, false), new("PRJ-006", 9, 0.38m, 1, false),
        new("PRJ-007", 12, 0.45m, 2, true), new("PRJ-008", 14, 0.55m, 2, false), new("PRJ-009", 10, 1.09m, 2, false),
        new("PRJ-010", 0, 0m, 1, false)
    ];

    private static readonly string[] CategoryCodes = ["LABOUR", "LABOUR", "LABOUR", "SOFTWARE", "SOFTWARE", "HARDWARE", "SERVICES", "SERVICES", "TRAVEL"];

    private static readonly Dictionary<string, string[]> Descriptions = new()
    {
        ["LABOUR"] = ["Contractor timesheets", "Overtime for release weekend", "Specialist consultant days"],
        ["SOFTWARE"] = ["Annual licence renewal", "Cloud subscription", "Test tooling seats"],
        ["HARDWARE"] = ["Test devices", "Network appliances", "Developer laptops"],
        ["SERVICES"] = ["Penetration test", "Managed hosting", "Vendor integration support"],
        ["TRAVEL"] = ["Site visit travel", "Vendor workshop travel", "Training travel"]
    };

    public static void Seed(SeedData data, Rng rng, Faker faker)
    {
        var vendors = Enumerable.Range(0, 12).Select(_ => faker.Company.CompanyName()).ToList();
        var id = 1L;

        foreach (var plan in Plans)
        {
            var project = data.ProjectByCode(plan.Project);
            var end = SeedClock.Min(project.ActualEndDate ?? project.PlannedEndDate, SeedClock.AsOf);
            var days = Math.Max(1, end.DayNumber - project.StartDate.DayNumber);

            if (plan.Posted > 0)
            {
                var target = Math.Round(SeedMath.CurrentBudget(data, project.Id, SeedClock.AsOf) * plan.Utilization, 2);
                var dates = Enumerable.Range(0, plan.Posted)
                    .Select(_ => project.StartDate.AddDays(rng.Between(0, days))).Order().ToList();
                var amounts = SeedMath.Split(target, dates.Select(_ => rng.Weight()).ToList());
                for (var i = 0; i < plan.Posted; i++)
                {
                    data.Costs.Add(NewCost(data, rng, vendors, project, id++, dates[i], amounts[i], posted: true));
                }
            }

            if (plan.Mistake)
            {
                var wrong = NewCost(data, rng, vendors, project, id++, new DateOnly(2026, 7, 10), 12_500m, posted: true);
                wrong.Description = "Duplicate licence invoice";
                data.Costs.Add(wrong);
                data.Costs.Add(Reversal(rng, project.OwnerId, project.Id, id++, wrong));
            }

            for (var i = 0; i < plan.Drafts; i++)
            {
                var date = SeedClock.Max(project.StartDate, new DateOnly(2026, 8, 15).AddDays(rng.Between(0, 44)));
                data.Costs.Add(NewCost(data, rng, vendors, project, id++, date, rng.Between(2_000, 60_000), posted: false));
            }
        }

        // PRJ-009: invoice dated after the cutoff. Posted, but must not count in spend as of 2026-09-30.
        var chatbot = data.ProjectByCode("PRJ-009");
        var future = NewCost(data, rng, vendors, chatbot, id++, new DateOnly(2026, 10, 5), 30_000m, posted: true);
        future.PostedAt = SeedClock.Utc(new DateOnly(2026, 9, 28), 15);
        future.CreatedAt = SeedClock.Utc(new DateOnly(2026, 9, 27), 10);
        data.Costs.Add(future);
        data.Costs.Add(NewCost(data, rng, vendors, chatbot, id, new DateOnly(2026, 10, 12), 18_000m, posted: false));
    }

    private static CostTransaction NewCost(SeedData data, Rng rng, List<string> vendors, Pmo.Domain.Portfolio.Project project,
        long id, DateOnly txnDate, decimal amount, bool posted)
    {
        var code = rng.Pick(CategoryCodes);
        var created = SeedClock.Utc(SeedClock.Min(txnDate, SeedClock.AsOf), 10);
        return new CostTransaction
        {
            Id = id, ProjectId = project.Id, CategoryId = data.Categories.Single(c => c.Code == code).Id, TxnDate = txnDate,
            Amount = amount, Description = rng.Pick(Descriptions[code]),
            VendorName = code == "LABOUR" ? null : rng.Pick(vendors),
            ReferenceNo = rng.Chance(0.85) ? $"INV-{rng.Between(10000, 99999)}" : null,
            Status = posted ? CostStatus.Posted : CostStatus.Draft, IdempotencyKey = rng.NextGuid(),
            CreatedById = project.OwnerId, CreatedAt = created,
            PostedById = posted ? project.OwnerId : null,
            PostedAt = posted ? SeedClock.Utc(SeedClock.Min(txnDate.AddDays(rng.Between(1, 5)), SeedClock.AsOf), 15) : null
        };
    }

    private static CostTransaction Reversal(Rng rng, int ownerId, int projectId, long id, CostTransaction original) => new()
    {
        Id = id, ProjectId = projectId, CategoryId = original.CategoryId, TxnDate = new DateOnly(2026, 7, 20),
        Amount = -original.Amount, Description = "Reversal: duplicate licence invoice", VendorName = original.VendorName,
        ReferenceNo = original.ReferenceNo, Status = CostStatus.Posted, ReversesTransactionId = original.Id,
        IdempotencyKey = rng.NextGuid(), CreatedById = ownerId, CreatedAt = SeedClock.Utc(new DateOnly(2026, 7, 20), 10),
        PostedById = ownerId, PostedAt = SeedClock.Utc(new DateOnly(2026, 7, 20), 15)
    };
}
