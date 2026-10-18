namespace Pmo.Domain.Finance;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class CostStatus
{
    public const string Draft = "draft";
    public const string Posted = "posted";

    public static readonly string[] All = [Draft, Posted];
}
