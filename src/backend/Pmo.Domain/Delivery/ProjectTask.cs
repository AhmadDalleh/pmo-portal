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
}
