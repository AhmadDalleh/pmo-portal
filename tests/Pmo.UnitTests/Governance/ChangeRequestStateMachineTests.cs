using Pmo.Domain.Common;
using Pmo.Domain.Delivery;
using Pmo.Domain.Governance;
using Shouldly;

namespace Pmo.UnitTests.Governance;

/// <summary>The change-request state machine (Epic E) and the risk status rule, one case per branch.</summary>
public class ChangeRequestStateMachineTests
{
    private const int Requester = 3, Reviewer = 4;
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);

    private static ChangeRequest In(string status) => new() { Id = 9, RequestedById = Requester, Status = status };

    [Fact]
    public void Submit_FromDraft_MovesToSubmittedWithOneHistoryRow()
    {
        var cr = In("draft");

        var history = cr.Submit(Requester, Now, "corr-1");

        (cr.Status, cr.SubmittedAt).ShouldBe(("submitted", Now));
        (history.FromStatus, history.ToStatus, history.ActorId, history.CorrelationId).ShouldBe(("draft", "submitted", Requester, "corr-1"));
    }

    [Fact]
    public void Submit_AfterReturn_ClearsTheDecisionFields()
    {
        var cr = In("returned");
        cr.DecidedById = Reviewer;
        cr.DecidedAt = Now;
        cr.DecisionReason = "More detail";

        cr.Submit(Requester, Now.AddDays(1), null);

        (cr.DecidedById, cr.DecidedAt, cr.DecisionReason).ShouldBe((null, null, null));
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("return")]
    public void Decide_Submitted_RecordsTheDecisionAndReturnsOneHistoryRow(string name)
    {
        var target = name switch { "approve" => "approved", "reject" => "rejected", _ => "returned" };
        var cr = In("submitted");

        var history = cr.Decide(target, Reviewer, "  Because  ", Now, null);

        (cr.Status, cr.DecidedById, cr.DecisionReason).ShouldBe((target, Reviewer, "Because"));
        (history.FromStatus, history.ToStatus, history.Reason).ShouldBe(("submitted", target, "Because"));
    }

    [Fact]
    public void Decide_ByTheRequester_IsForbidden()
    {
        var ex = Should.Throw<DomainRuleException>(() => In("submitted").Decide("approved", Requester, "ok", Now, null));

        (ex.Code, ex.IsForbidden).ShouldBe(("self_approval_forbidden", true));
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("approved")]
    [InlineData("rejected")]
    [InlineData("withdrawn")]
    [InlineData("returned")]
    public void Decide_WhenNotSubmitted_IsAnInvalidTransition(string status)
    {
        Should.Throw<DomainRuleException>(() => In(status).Decide("approved", Reviewer, "ok", Now, null)).Code.ShouldBe("invalid_transition");
    }

    [Fact]
    public void Decide_WithoutReasonOrWithUnknownDecision_IsAValidationError()
    {
        Should.Throw<DomainRuleException>(() => In("submitted").Decide("approved", Reviewer, " ", Now, null)).Code.ShouldBe("reason_required");
        Should.Throw<DomainRuleException>(() => In("submitted").Decide("submitted", Reviewer, "ok", Now, null)).Code.ShouldBe("invalid_decision");
    }

    [Theory]
    [InlineData("draft", true)]
    [InlineData("returned", true)]
    [InlineData("submitted", false)]
    [InlineData("approved", false)]
    [InlineData("rejected", false)]
    [InlineData("withdrawn", false)]
    public void Withdraw_IsAllowedOnlyFromDraftOrReturned(string status, bool allowed)
    {
        var cr = In(status);

        if (allowed)
        {
            cr.Withdraw(Requester, "no longer needed", Now, null);
            cr.Status.ShouldBe("withdrawn");
        }
        else
        {
            Should.Throw<DomainRuleException>(() => cr.Withdraw(Requester, null, Now, null)).Code.ShouldBe("invalid_transition");
        }
    }

    [Fact]
    public void SubmitWithdrawAndEdit_ByAnotherUser_AreForbidden()
    {
        Should.Throw<DomainRuleException>(() => In("draft").Submit(Reviewer, Now, null)).Code.ShouldBe("only_requester");
        Should.Throw<DomainRuleException>(() => In("draft").Withdraw(Reviewer, null, Now, null)).Code.ShouldBe("only_requester");
        Should.Throw<DomainRuleException>(() => In("draft").EnsureEditableBy(Reviewer)).Code.ShouldBe("only_requester");
        Should.Throw<DomainRuleException>(() => In("submitted").EnsureEditableBy(Requester)).Code.ShouldBe("not_editable");
        Should.NotThrow(() => In("returned").EnsureEditableBy(Requester));
    }

    [Fact]
    public void Risk_ApplyStatus_ClosingStampsClosedAtAndReopeningClearsIt()
    {
        var risk = new Risk { Probability = 4, Impact = 4 };

        risk.ApplyStatus("closed", Now);
        risk.ClosedAt.ShouldBe(Now);
        risk.ApplyStatus("mitigating", Now.AddDays(1));

        risk.ClosedAt.ShouldBeNull();
        risk.IsHighExposure.ShouldBeTrue();
        Should.Throw<DomainRuleException>(() => risk.ApplyStatus("gone", Now)).Code.ShouldBe("invalid_status");
    }
}
