namespace Pmo.Application.Features.Tasks.Dtos;

/// <summary>
/// A task as the UI shows it. Indicator is "overdue", "due_today", "due_soon" (within 14 days) or "" (story C4),
/// worked out on the server for the requested as-of date; the UI only draws the icon and text.
/// </summary>
public record TaskDto(
    int Id, int ProjectId, string ProjectCode, int? MilestoneId, string? MilestoneName, string Title, string? Description,
    int? AssigneeId, string? AssigneeName, string Status, string Priority, DateOnly? StartDate, DateOnly DueDate,
    int Weight, int ProgressPct, DateTimeOffset? CompletedAt, string Indicator, uint Version);
