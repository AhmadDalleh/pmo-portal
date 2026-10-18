namespace Pmo.Domain.Delivery;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class RiskType
{
    public const string Risk = "risk";
    public const string Issue = "issue";

    public static readonly string[] All = [Risk, Issue];
}
