using Pmo.Domain.Finance;

namespace DataGenerator.Generation;

/// <summary>
/// Monthly forecasts from October 2026 to the project's planned end. PRJ-004 (finished) and PRJ-005 (the
/// "no forecast data" case) get none. The totals are chosen so EAC lands where each project's edge case needs it.
/// </summary>
public static class ForecastSeeder
{
    private static readonly Dictionary<string, decimal> ForecastToComplete = new()
    {
        ["PRJ-001"] = 90_000m, ["PRJ-002"] = 1_300_000m, ["PRJ-003"] = 500_000m, ["PRJ-006"] = 200_000m,
        ["PRJ-007"] = 700_000m, ["PRJ-008"] = 700_000m, ["PRJ-009"] = 60_000m, ["PRJ-010"] = 600_000m
    };

    // Typical share of each category (LABOUR, SOFTWARE, HARDWARE, SERVICES, TRAVEL).
    private static readonly decimal[] CategoryShare = [0.50m, 0.15m, 0.10m, 0.20m, 0.05m];

    public static void Seed(SeedData data, Rng rng)
    {
        var id = 1;
        foreach (var (code, total) in ForecastToComplete.OrderBy(kv => kv.Key))
        {
            var project = data.ProjectByCode(code);
            var months = new List<DateOnly>();
            for (var m = new DateOnly(2026, 10, 1); m <= project.PlannedEndDate; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            var cells = months.SelectMany(month => data.Categories.Select((category, i) => (month, category, i))).ToList();
            var amounts = SeedMath.Split(total, cells.Select(c => CategoryShare[c.i] * rng.Weight()).ToList());

            for (var i = 0; i < cells.Count; i++)
            {
                data.Forecasts.Add(new FinancialForecast
                {
                    Id = id++, ProjectId = project.Id, CategoryId = cells[i].category.Id, PeriodMonth = cells[i].month,
                    Amount = amounts[i], UpdatedById = project.OwnerId, UpdatedAt = SeedClock.Utc(new DateOnly(2026, 9, 15))
                });
            }
        }
    }
}
