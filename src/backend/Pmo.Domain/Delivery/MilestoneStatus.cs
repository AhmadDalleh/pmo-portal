namespace Pmo.Domain.Delivery;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class MilestoneStatus
{
    public const string Pending = "pending";
    public const string Completed = "completed";

    public static readonly string[] All = [Pending, Completed];
}
