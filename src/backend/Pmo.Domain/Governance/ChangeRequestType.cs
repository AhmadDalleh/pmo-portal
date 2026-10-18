namespace Pmo.Domain.Governance;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class ChangeRequestType
{
    public const string Scope = "scope";
    public const string Schedule = "schedule";
    public const string Budget = "budget";
    public const string Resource = "resource";

    public static readonly string[] All = [Scope, Schedule, Budget, Resource];
}
