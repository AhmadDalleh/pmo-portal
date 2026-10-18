namespace Pmo.Domain.Finance;

/// <summary>A signed change to the budget; counts only when approved and EffectiveDate &lt;= asOf.</summary>
public class BudgetRevision
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public string Status { get; set; } = RevisionStatus.Pending;
    public int? ChangeRequestId { get; set; }
    public int RequestedById { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public int? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    public uint Version { get; set; }
}
