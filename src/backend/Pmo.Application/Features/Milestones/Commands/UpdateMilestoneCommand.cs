namespace Pmo.Application.Features.Milestones.Commands;

public record UpdateMilestoneCommand(string Name, string? Description, DateOnly DueDate, uint Version);
