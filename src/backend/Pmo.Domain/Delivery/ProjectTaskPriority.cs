namespace Pmo.Domain.Delivery;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class ProjectTaskPriority
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";

    public static readonly string[] All = [Low, Medium, High, Critical];
}
