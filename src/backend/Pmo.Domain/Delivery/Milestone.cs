using Pmo.Domain.Common;

namespace Pmo.Domain.Delivery;

public class Milestone : AuditableEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = MilestoneStatus.Pending;
    public DateOnly? CompletedAt { get; set; }
}
