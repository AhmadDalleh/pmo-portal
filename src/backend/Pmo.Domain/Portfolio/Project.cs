using Pmo.Domain.Common;

namespace Pmo.Domain.Portfolio;

public class Project : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DepartmentId { get; set; }

    /// <summary>User id of the owning Project Manager (role checked by the service).</summary>
    public int OwnerId { get; set; }

    public string Status { get; set; } = ProjectStatus.Proposed;
    public DateOnly StartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }
}
