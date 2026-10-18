using Pmo.Domain.Common;

namespace Pmo.Domain.Governance;

public class ChangeRequest : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = ChangeRequestType.Scope;

    /// <summary>Informational only; never changes the budget by itself.</summary>
    public decimal? CostImpact { get; set; }

    public int? ScheduleImpactDays { get; set; }
    public string Status { get; set; } = ChangeRequestStatus.Draft;
    public int RequestedById { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public int? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }

    // The state machine of user-stories.md (Epic E). Anything not listed is an invalid transition (409).
    private static readonly Dictionary<string, string[]> Allowed = new()
    {
        [ChangeRequestStatus.Draft] = [ChangeRequestStatus.Submitted, ChangeRequestStatus.Withdrawn],
        [ChangeRequestStatus.Submitted] = [ChangeRequestStatus.Approved, ChangeRequestStatus.Rejected, ChangeRequestStatus.Returned],
        [ChangeRequestStatus.Returned] = [ChangeRequestStatus.Submitted, ChangeRequestStatus.Withdrawn]
    };

    /// <summary>Only a draft or returned request can be edited, and only by the person who raised it.</summary>
    public void EnsureEditableBy(int actorId)
    {
        EnsureRequester(actorId);
        if (Status is not (ChangeRequestStatus.Draft or ChangeRequestStatus.Returned))
        {
            throw new DomainRuleException("not_editable", $"A {Status} change request cannot be edited.");
        }
    }

    /// <summary>draft or returned -> submitted. A resubmission clears the previous decision (the history keeps it).</summary>
    public ChangeRequestHistory Submit(int actorId, DateTimeOffset now, string? correlationId)
    {
        EnsureRequester(actorId);
        var from = MoveTo(ChangeRequestStatus.Submitted);
        SubmittedAt = now;
        DecidedById = null;
        DecidedAt = null;
        DecisionReason = null;
        return History(from, ChangeRequestStatus.Submitted, actorId, null, now, correlationId);
    }

    /// <summary>draft or returned -> withdrawn (terminal).</summary>
    public ChangeRequestHistory Withdraw(int actorId, string? reason, DateTimeOffset now, string? correlationId)
    {
        EnsureRequester(actorId);
        var from = MoveTo(ChangeRequestStatus.Withdrawn);
        return History(from, ChangeRequestStatus.Withdrawn, actorId, reason?.Trim(), now, correlationId);
    }

    /// <summary>
    /// Story E3: submitted -> approved, rejected or returned, with a reason. The requester can never decide their own request
    /// (the database CHECK says the same). Returns the one history row that must be saved together with this change.
    /// </summary>
    public ChangeRequestHistory Decide(string decision, int actorId, string reason, DateTimeOffset now, string? correlationId)
    {
        if (decision is not (ChangeRequestStatus.Approved or ChangeRequestStatus.Rejected or ChangeRequestStatus.Returned))
        {
            throw new DomainRuleException("invalid_decision", "Decision must be approve, reject or return.", isValidation: true);
        }

        if (actorId == RequestedById)
        {
            throw new DomainRuleException("self_approval_forbidden", "You cannot decide your own change request.", isForbidden: true);
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("reason_required", "A decision reason is required.", isValidation: true);
        }

        var from = MoveTo(decision);
        DecidedById = actorId;
        DecidedAt = now;
        DecisionReason = reason.Trim();
        return History(from, decision, actorId, DecisionReason, now, correlationId);
    }

    /// <summary>The first history row of a new request (null -> draft).</summary>
    public ChangeRequestHistory CreationHistory(DateTimeOffset now, string? correlationId) =>
        History(null, ChangeRequestStatus.Draft, RequestedById, null, now, correlationId);

    private string MoveTo(string target)
    {
        if (!Allowed.TryGetValue(Status, out var targets) || !targets.Contains(target))
        {
            throw new DomainRuleException("invalid_transition", $"A {Status} change request cannot become {target}.");
        }

        var from = Status;
        Status = target;
        return from;
    }

    private void EnsureRequester(int actorId)
    {
        if (actorId != RequestedById)
        {
            throw new DomainRuleException("only_requester", "Only the person who raised this change request can do that.", isForbidden: true);
        }
    }

    private ChangeRequestHistory History(string? from, string to, int actorId, string? reason, DateTimeOffset now, string? correlationId) => new()
    {
        ChangeRequestId = Id, FromStatus = from, ToStatus = to, ActorId = actorId, Reason = reason, OccurredAt = now, CorrelationId = correlationId
    };
}
