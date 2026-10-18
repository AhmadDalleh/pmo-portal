using Pmo.Domain.Finance;

namespace DataGenerator.Generation;

/// <summary>
/// The KPI formulas of design doc §6, written once in C# for the generator only: it needs them to aim spend at a
/// utilisation target and to assert the edge cases. The API never uses this; it computes KPIs in SQL.
/// </summary>
public static class SeedMath
{
    public static decimal CurrentBudget(SeedData data, int projectId, DateOnly asOf) =>
        data.Budgets.Single(b => b.ProjectId == projectId).InitialAmount
        + data.Revisions.Where(r => r.ProjectId == projectId && r.Status == RevisionStatus.Approved && r.EffectiveDate <= asOf)
            .Sum(r => r.Amount);

    public static decimal ActualSpend(SeedData data, int projectId, DateOnly asOf) =>
        data.Costs.Where(c => c.ProjectId == projectId && c.Status == CostStatus.Posted && c.TxnDate <= asOf)
            .Sum(c => c.Amount);

    public static decimal ForecastToComplete(SeedData data, int projectId, DateOnly asOf) =>
        data.Forecasts.Where(f => f.ProjectId == projectId && f.PeriodMonth > SeedClock.MonthStart(asOf))
            .Sum(f => f.Amount);

    /// <summary>Weighted progress of non-cancelled tasks; null when the project has none.</summary>
    public static decimal? Progress(SeedData data, int projectId)
    {
        var tasks = data.Tasks.Where(t => t.ProjectId == projectId && t.Status != "cancelled").ToList();
        var weight = tasks.Sum(t => (decimal)t.Weight);
        return weight == 0 ? null : tasks.Sum(t => t.Weight * (decimal)t.ProgressPct) / weight;
    }

    /// <summary>Splits total into parts proportional to the weights; rounding differences go to the last part.</summary>
    public static List<decimal> Split(decimal total, IReadOnlyList<decimal> weights)
    {
        var sum = weights.Sum();
        var parts = weights.Select(w => Math.Round(total * w / sum, 2)).ToList();
        parts[^1] = total - parts.Take(parts.Count - 1).Sum();
        return parts;
    }
}
