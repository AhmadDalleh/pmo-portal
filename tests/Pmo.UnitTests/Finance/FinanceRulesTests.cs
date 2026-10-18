using Pmo.Domain.Common;
using Pmo.Domain.Finance;
using Shouldly;

namespace Pmo.UnitTests.Finance;

/// <summary>Domain rules of cost transactions (F5-F7) and budget revisions (F4), one case per branch.</summary>
public class FinanceRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);

    private static CostTransaction Draft() => new() { Id = 7, ProjectId = 1, CategoryId = 1, Amount = 1_000m, Status = CostStatus.Draft, TxnDate = new DateOnly(2026, 8, 1) };

    private static CostTransaction Posted()
    {
        var cost = Draft();
        cost.Post(3, Now);
        return cost;
    }

    [Fact]
    public void Post_Draft_BecomesPostedWithActorAndTime()
    {
        var cost = Draft();

        cost.Post(3, Now);

        (cost.Status, cost.PostedById, cost.PostedAt).ShouldBe((CostStatus.Posted, 3, Now));
    }

    [Fact]
    public void Post_Twice_ThrowsAlreadyPosted()
    {
        Should.Throw<DomainRuleException>(() => Posted().Post(3, Now)).Code.ShouldBe("already_posted");
    }

    [Fact]
    public void EnsureDraft_OnPostedCost_ThrowsPostedImmutable()
    {
        Should.Throw<DomainRuleException>(() => Posted().EnsureDraft()).Code.ShouldBe("posted_immutable");
        Should.NotThrow(() => Draft().EnsureDraft());
    }

    [Fact]
    public void Reverse_Posted_ReturnsNegativePostedLinkedRowWithSameDate()
    {
        var original = Posted();

        var reversal = original.Reverse("Duplicate invoice", 4, Now.AddDays(1), Guid.NewGuid());

        (reversal.Amount, reversal.Status, reversal.ReversesTransactionId, reversal.TxnDate).ShouldBe((-1_000m, CostStatus.Posted, 7L, original.TxnDate));
        reversal.Description.ShouldContain("Duplicate invoice");
        original.Amount.ShouldBe(1_000m);   // the original is not touched
    }

    [Fact]
    public void Reverse_DraftOrReversalOrWithoutReason_IsRefused()
    {
        var reversal = Posted().Reverse("x", 4, Now, Guid.NewGuid());
        reversal.Id = 8;

        Should.Throw<DomainRuleException>(() => Draft().Reverse("x", 4, Now, Guid.NewGuid())).Code.ShouldBe("not_posted");
        Should.Throw<DomainRuleException>(() => reversal.Reverse("x", 4, Now, Guid.NewGuid())).Code.ShouldBe("cannot_reverse_reversal");
        Should.Throw<DomainRuleException>(() => Posted().Reverse("  ", 4, Now, Guid.NewGuid())).Code.ShouldBe("reason_required");
    }

    private static BudgetRevision Pending() => new() { Status = RevisionStatus.Pending, RequestedById = 3, Amount = 100m };

    [Fact]
    public void Decide_ByAnotherUser_RecordsDecision()
    {
        var revision = Pending();

        revision.Decide(RevisionStatus.Approved, 1, "Agreed", Now);

        (revision.Status, revision.DecidedById, revision.DecidedAt, revision.DecisionReason).ShouldBe((RevisionStatus.Approved, 1, Now, "Agreed"));
    }

    [Fact]
    public void Decide_OwnRequest_IsForbidden()
    {
        var ex = Should.Throw<DomainRuleException>(() => Pending().Decide(RevisionStatus.Approved, 3, "Me", Now));

        (ex.Code, ex.IsForbidden).ShouldBe(("self_approval_forbidden", true));
    }

    [Theory]
    [InlineData("maybe", "ok", "invalid_decision")]
    [InlineData("approved", " ", "reason_required")]
    public void Decide_InvalidInput_ThrowsValidationRule(string decision, string reason, string code)
    {
        var ex = Should.Throw<DomainRuleException>(() => Pending().Decide(decision, 1, reason, Now));

        (ex.Code, ex.IsValidation).ShouldBe((code, true));
    }

    [Fact]
    public void Decide_AlreadyDecided_ThrowsInvalidTransition()
    {
        var revision = Pending();
        revision.Decide(RevisionStatus.Rejected, 1, "No", Now);

        Should.Throw<DomainRuleException>(() => revision.Decide(RevisionStatus.Approved, 2, "Yes", Now)).Code.ShouldBe("invalid_transition");
    }
}
