namespace Pmo.Application.Features.Milestones.Commands;

public record CreateMilestoneCommand(string Name, string? Description, DateOnly DueDate);
