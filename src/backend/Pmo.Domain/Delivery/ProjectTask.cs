using Pmo.Domain.Common;

namespace Pmo.Domain.Delivery;

/// <summary>A task (table "tasks"). Named ProjectTask to avoid clashing with System.Threading.Tasks.Task.</summary>
public class ProjectTask : AuditableEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }

    /// <summary>Optional; the composite FK forces the milestone to belong to the same project.</summary>
    public int? MilestoneId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? AssigneeId { get; set; }
    public string Status { get; set; } = ProjectTaskStatus.Todo;
    public string Priority { get; set; } = ProjectTaskPriority.Medium;
    public DateOnly? StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public short Weight { get; set; } = 1;
    public short ProgressPct { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// The one place that changes status and progress together (stories C1, C2).
    /// Done forces progress to 100 and stamps completed_at; leaving Done clears completed_at.
    /// </summary>
    public void ApplyProgress(string status, short progressPct, DateTimeOffset now)
    {
        if (!ProjectTaskStatus.All.Contains(status))
        {
            throw new DomainRuleException("invalid_status", $"Unknown task status '{status}'.", isValidation: true);
        }

        if (progressPct is < 0 or > 100)
        {
            throw new DomainRuleException("invalid_progress", "Progress must be between 0 and 100.", isValidation: true);
        }

        var wasDone = Status == ProjectTaskStatus.Done;
        Status = status;
        ProgressPct = status == ProjectTaskStatus.Done ? (short)100 : progressPct;

        if (status == ProjectTaskStatus.Done)
        {
            CompletedAt ??= now;
        }
        else if (wasDone)
        {
            CompletedAt = null;
        }
    }
}
