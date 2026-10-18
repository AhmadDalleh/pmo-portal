namespace Pmo.Domain.Governance;

/// <summary>Allowed values of the matching status/type column; the DB CHECK constraint is built from <see cref="All"/>.</summary>
public static class ChangeRequestStatus
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Returned = "returned";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Withdrawn = "withdrawn";

    public static readonly string[] All = [Draft, Submitted, Returned, Approved, Rejected, Withdrawn];
}
