namespace Pmo.Domain.Automation;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class RunStatus
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Partial = "partial";
    public const string Failed = "failed";

    public static readonly string[] All = [Running, Succeeded, Partial, Failed];
}
