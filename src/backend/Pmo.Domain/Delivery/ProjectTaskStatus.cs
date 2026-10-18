namespace Pmo.Domain.Delivery;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class ProjectTaskStatus
{
    public const string Todo = "todo";
    public const string InProgress = "in_progress";
    public const string Blocked = "blocked";
    public const string Done = "done";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Todo, InProgress, Blocked, Done, Cancelled];
}
