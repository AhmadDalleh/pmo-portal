namespace Pmo.Application.Features.Projects.Dtos;

public record NextMilestoneDto(int Id, string Name, DateOnly DueDate, bool IsOverdue);
