namespace Pmo.Domain.Portfolio;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class MemberRole
{
    public const string Manager = "manager";
    public const string Contributor = "contributor";

    public static readonly string[] All = [Manager, Contributor];
}
