namespace Pmo.Application.Features.Projects.Commands;

/// <summary>B2. Explicit fields only (no over-posting): code, department and budget cannot be changed here.</summary>
public record UpdateProjectCommand(
    string Name, string? Description, int OwnerId, string Status,
    DateOnly StartDate, DateOnly PlannedEndDate, DateOnly? ActualEndDate, uint Version);
