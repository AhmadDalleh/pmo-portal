namespace Pmo.Application.Features.Tasks.Commands;

/// <summary>C1. Same fields plus the task's current Version.</summary>
public record UpdateTaskCommand(
    string Title, string? Description, int? AssigneeId, int? MilestoneId, string Status, string Priority,
    DateOnly? StartDate, DateOnly DueDate, int Weight, int ProgressPct, uint Version);
