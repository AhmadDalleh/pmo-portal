namespace Pmo.Application.Features.Projects.Dtos;

/// <summary>A project as stored. Version must be sent back on update (optimistic concurrency).</summary>
public record ProjectDto(
    int Id, string Code, string Name, string? Description,
    int DepartmentId, string DepartmentName, int OwnerId, string OwnerName,
    string Status, DateOnly StartDate, DateOnly PlannedEndDate, DateOnly? ActualEndDate,
    decimal InitialBudget, uint Version);
