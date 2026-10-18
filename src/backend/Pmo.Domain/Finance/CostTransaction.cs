using Pmo.Domain.Common;

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

    /// <summary>Drafts can be edited or deleted; posted rows never (the database trigger says the same).</summary>
    public void EnsureDraft()
    {
        if (Status == CostStatus.Posted)
        {
            throw new DomainRuleException("posted_immutable", "A posted cost cannot be changed or deleted. Create a reversal instead.");
        }
    }

    /// <summary>Story F6: draft becomes posted and starts to count toward actual spend.</summary>
    public void Post(int actorId, DateTimeOffset now)
    {
        if (Status == CostStatus.Posted)
        {
            throw new DomainRuleException("already_posted", "This cost is already posted.");
        }

        Status = CostStatus.Posted;
        PostedById = actorId;
        PostedAt = now;
    }

    /// <summary>
    /// Story F7: the correction is a new posted row with the negative amount, dated like the original so the two net
    /// out in every period. The original is never touched.
    /// </summary>
    public CostTransaction Reverse(string reason, int actorId, DateTimeOffset now, Guid idempotencyKey)
    {
        if (Status != CostStatus.Posted)
        {
            throw new DomainRuleException("not_posted", "Only posted costs can be reversed. Delete a draft instead.");
        }

        if (ReversesTransactionId is not null)
        {
            throw new DomainRuleException("cannot_reverse_reversal", "A reversal cannot be reversed.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("reason_required", "A reason is required.", isValidation: true);
        }

        var description = $"Reversal of #{Id}: {reason.Trim()}";
        return new CostTransaction
        {
            ProjectId = ProjectId, CategoryId = CategoryId, TxnDate = TxnDate, Amount = -Amount,
            Description = description.Length > 300 ? description[..300] : description, VendorName = VendorName, ReferenceNo = ReferenceNo,
            Status = CostStatus.Posted, ReversesTransactionId = Id, IdempotencyKey = idempotencyKey,
            CreatedById = actorId, CreatedAt = now, PostedById = actorId, PostedAt = now
        };
    }
}
