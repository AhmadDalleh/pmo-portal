using Pmo.Application.Features.Tasks.Queries;
using Pmo.Domain.Common;
using Pmo.Domain.Delivery;
using Shouldly;

namespace Pmo.UnitTests.Delivery;

/// <summary>Domain rules of tasks and milestones (C1, C2, C4, C5), one case per branch.</summary>
public class TaskRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly AsOf = new(2026, 9, 30);

    private static ProjectTask NewTask(string status = "todo", short progress = 0, DateOnly? due = null) =>
        new() { Status = status, ProgressPct = progress, DueDate = due ?? AsOf };

    [Fact]
    public void ApplyProgress_ToDone_ForcesProgress100AndStampsCompletedAt()
    {
        var task = NewTask();

        task.ApplyProgress("done", 10, Now);

        task.ProgressPct.ShouldBe((short)100);
        task.CompletedAt.ShouldBe(Now);
    }

    [Fact]
    public void ApplyProgress_DoneAgain_KeepsTheOriginalCompletionTime()
    {
        var task = NewTask();
        task.ApplyProgress("done", 100, Now);

        task.ApplyProgress("done", 100, Now.AddDays(3));

        task.CompletedAt.ShouldBe(Now);
    }

    [Fact]
    public void ApplyProgress_LeavingDone_ClearsCompletedAtAndKeepsGivenProgress()
    {
        var task = NewTask();
        task.ApplyProgress("done", 100, Now);

        task.ApplyProgress("in_progress", 60, Now.AddDays(1));

        task.CompletedAt.ShouldBeNull();
        task.ProgressPct.ShouldBe((short)60);
    }

    [Theory]
    [InlineData("finished", 10, "invalid_status")]
    [InlineData("todo", 101, "invalid_progress")]
    [InlineData("todo", -1, "invalid_progress")]
    public void ApplyProgress_InvalidInput_ThrowsValidationRule(string status, int progress, string code)
    {
        var ex = Should.Throw<DomainRuleException>(() => NewTask().ApplyProgress(status, (short)progress, Now));

        ex.Code.ShouldBe(code);
        ex.IsValidation.ShouldBeTrue();
    }

    [Theory]
    [InlineData("todo", "2026-09-29", "overdue")]
    [InlineData("in_progress", "2026-09-30", "due_today")]     // due ON the as-of date is not overdue
    [InlineData("blocked", "2026-10-14", "due_soon")]          // as-of + 14 is still "soon"
    [InlineData("todo", "2026-10-15", "")]
    [InlineData("done", "2026-09-01", "")]                     // finished work has no indicator
    [InlineData("cancelled", "2026-09-01", "")]
    public void Indicator_FollowsTheDateRules(string status, string due, string expected)
    {
        TaskRules.Indicator(NewTask(status, due: DateOnly.Parse(due)), AsOf).ShouldBe(expected);
    }

    [Fact]
    public void Complete_Milestone_SetsDateAndSecondCallIsAConflict()
    {
        var milestone = new Milestone { Status = MilestoneStatus.Pending };

        milestone.Complete(AsOf);

        milestone.Status.ShouldBe(MilestoneStatus.Completed);
        milestone.CompletedAt.ShouldBe(AsOf);
        Should.Throw<DomainRuleException>(() => milestone.Complete(AsOf)).Code.ShouldBe("already_completed");
    }
}
