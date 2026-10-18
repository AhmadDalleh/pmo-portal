namespace Pmo.Domain.Delivery;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class RiskStatus
{
    public const string Open = "open";
    public const string Mitigating = "mitigating";
    public const string Closed = "closed";

    public static readonly string[] All = [Open, Mitigating, Closed];
}
