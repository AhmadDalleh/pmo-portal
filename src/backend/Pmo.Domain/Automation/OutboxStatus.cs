namespace Pmo.Domain.Automation;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class OutboxStatus
{
    public const string Pending = "pending";
    public const string Processed = "processed";
    public const string Failed = "failed";

    public static readonly string[] All = [Pending, Processed, Failed];
}
