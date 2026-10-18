namespace Pmo.Application.Features.Projects.Commands;

/// <summary>B1. OwnerId is optional: a Project Manager always becomes the owner; an Administrator must name a PM.</summary>
public record CreateProjectCommand(
    string Code, string Name, string? Description, int DepartmentId, int? OwnerId,
    DateOnly StartDate, DateOnly PlannedEndDate, decimal InitialBudget);
