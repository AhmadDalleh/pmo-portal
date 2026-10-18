namespace Pmo.Domain.Finance;

/// <summary>
/// A cost line. Drafts can change; posted rows are immutable (DB trigger) and are
/// corrected by a negative reversal row pointing at the original.
/// </summary>
public class CostTransaction
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public int CategoryId { get; set; }
    public DateOnly TxnDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? VendorName { get; set; }
    public string? ReferenceNo { get; set; }
    public string Status { get; set; } = CostStatus.Draft;
    public long? ReversesTransactionId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public int CreatedById { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? PostedById { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public uint Version { get; set; }
}
