using Pmo.Domain.Common;

namespace Pmo.Domain.Governance;

public class ChangeRequest : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = ChangeRequestType.Scope;

    /// <summary>Informational only; never changes the budget by itself.</summary>
    public decimal? CostImpact { get; set; }

    public int? ScheduleImpactDays { get; set; }
    public string Status { get; set; } = ChangeRequestStatus.Draft;
    public int RequestedById { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public int? DecidedById { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}
