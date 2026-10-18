using Pmo.Domain.Common;

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

    /// <summary>
    /// Story F4. Only a pending revision can be decided, never by the person who asked for it, and always with a reason.
    /// Approving does not change anything else: the budget KPI simply starts to include it from its effective date.
    /// </summary>
    public void Decide(string decision, int actorId, string reason, DateTimeOffset now)
    {
        if (decision is not (RevisionStatus.Approved or RevisionStatus.Rejected))
        {
            throw new DomainRuleException("invalid_decision", "Decision must be approved or rejected.", isValidation: true);
        }

        if (actorId == RequestedById)
        {
            throw new DomainRuleException("self_approval_forbidden", "You cannot decide your own budget revision.", isForbidden: true);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("reason_required", "A decision reason is required.", isValidation: true);
        }

        if (Status != RevisionStatus.Pending)
        {
            throw new DomainRuleException("invalid_transition", $"This revision is already {Status}.");
        }

        Status = decision;
        DecidedById = actorId;
        DecidedAt = now;
        DecisionReason = reason.Trim();
    }
}
