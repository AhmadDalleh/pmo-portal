namespace Pmo.Application.Features.Milestones.Dtos;

/// <summary>A milestone with how much of its work is done. IsOverdue = still pending and due before the as-of date.</summary>
public record MilestoneDto(
    int Id, int ProjectId, string Name, string? Description, DateOnly DueDate, string Status, DateOnly? CompletedAt,
    bool IsOverdue, int TasksTotal, int TasksDone, uint Version);
