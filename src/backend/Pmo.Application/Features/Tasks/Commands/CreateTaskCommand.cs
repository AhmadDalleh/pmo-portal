namespace Pmo.Application.Features.Tasks.Commands;

/// <summary>C1. Done forces progress to 100.</summary>
public record CreateTaskCommand(
    string Title, string? Description, int? AssigneeId, int? MilestoneId, string Status, string Priority,
    DateOnly? StartDate, DateOnly DueDate, int Weight, int ProgressPct);
