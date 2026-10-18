using System.Linq.Expressions;
using Pmo.Domain.Delivery;

namespace Pmo.Application.Features.Tasks.Queries;

/// <summary>The task date rules of story C4, in one place so every list agrees.</summary>
public static class TaskRules
{
    public const int DueSoonDays = 14;

    /// <summary>Due before the as-of date and still open. A task due ON the as-of date is "due today", not overdue.</summary>
    public static Expression<Func<ProjectTask, bool>> Overdue(DateOnly asOf) =>
        t => t.DueDate < asOf && t.Status != ProjectTaskStatus.Done && t.Status != ProjectTaskStatus.Cancelled;

    public static string Indicator(ProjectTask task, DateOnly asOf)
    {
        if (task.Status is ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled)
        {
            return string.Empty;
        }

        if (task.DueDate < asOf)
        {
            return "overdue";
        }

        if (task.DueDate == asOf)
        {
            return "due_today";
        }

        return task.DueDate <= asOf.AddDays(DueSoonDays) ? "due_soon" : string.Empty;
    }
}
