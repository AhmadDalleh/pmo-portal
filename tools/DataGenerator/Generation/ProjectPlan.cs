namespace DataGenerator.Generation;

/// <summary>The hand-written plan of one demo project: what it is and which edge case it carries (design doc §10).</summary>
public record ProjectPlan(
    string Code, string Name, int DepartmentId, int OwnerPm, string Status,
    DateOnly Start, DateOnly PlannedEnd, DateOnly? ActualEnd, decimal InitialBudget,
    int TaskCount, int MilestoneCount, string EdgeCase)
{
    public static readonly ProjectPlan[] All =
    [
        new("PRJ-001", "Customer Self-Service App Revamp", 1, 1, "active", D(2025, 10, 1), D(2027, 3, 31), null, 1_200_000m, 14, 4, "near budget"),
        new("PRJ-002", "Network Monitoring Platform", 2, 2, "active", D(2025, 9, 1), D(2027, 6, 30), null, 2_400_000m, 16, 4, "forecast overrun"),
        new("PRJ-003", "SD-WAN Branch Pilot", 2, 2, "active", D(2026, 3, 1), D(2027, 2, 28), null, 800_000m, 12, 3, "no posted spend"),
        new("PRJ-004", "Billing Data Migration", 3, 3, "completed", D(2025, 7, 1), D(2026, 6, 30), D(2026, 7, 15), 900_000m, 12, 4, "completed project"),
        new("PRJ-005", "B2B Ordering Portal", 3, 3, "active", D(2026, 1, 15), D(2027, 1, 31), null, 650_000m, 12, 3, "no forecast data"),
        new("PRJ-006", "Data Lake Phase 1", 1, 1, "on_hold", D(2025, 11, 1), D(2027, 1, 31), null, 1_000_000m, 12, 3, "blocked tasks, closed high risk"),
        new("PRJ-007", "Field Operations Mobile App", 2, 4, "active", D(2026, 2, 1), D(2027, 3, 31), null, 1_500_000m, 14, 3, "healthy baseline"),
        new("PRJ-008", "CRM Integration Layer", 3, 5, "active", D(2025, 12, 1), D(2027, 4, 30), null, 1_800_000m, 14, 4, "approved + pending revision"),
        new("PRJ-009", "Virtual Assistant Chatbot", 1, 1, "active", D(2026, 4, 1), D(2027, 2, 28), null, 500_000m, 12, 3, "post-cutoff cost, overspent"),
        new("PRJ-010", "Fibre Rollout Planning Tool", 2, 4, "proposed", D(2026, 9, 1), D(2027, 9, 30), null, 700_000m, 0, 2, "budget, no tasks")
    ];

    private static DateOnly D(int y, int m, int d) => new(y, m, d);
}
