namespace Pmo.Domain.Portfolio;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class ProjectStatus
{
    public const string Proposed = "proposed";
    public const string Active = "active";
    public const string OnHold = "on_hold";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static readonly string[] All = [Proposed, Active, OnHold, Completed, Cancelled];
}
