using Pmo.Domain.Common;

namespace Pmo.Domain.Delivery;

/// <summary>Risks and issues share one table (Type = risk | issue).</summary>
public class Risk : AuditableEntity
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Type { get; set; } = RiskType.Risk;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public short Probability { get; set; }
    public short Impact { get; set; }

    /// <summary>Computed by PostgreSQL (probability * impact, stored). Read-only in code.</summary>
    public short Score { get; private set; }

    public string? Mitigation { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = RiskStatus.Open;
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>Exposure score at which a risk needs a mitigation plan and counts as "high exposure" (database-design.md section 3.2).</summary>
    public const int HighExposureThreshold = 15;

    public bool IsHighExposure => Probability * Impact >= HighExposureThreshold;

    /// <summary>Closing stamps closed_at (the CHECK needs both together); reopening clears it.</summary>
    public void ApplyStatus(string status, DateTimeOffset now)
    {
        if (!RiskStatus.All.Contains(status))
        {
            throw new DomainRuleException("invalid_status", $"Unknown risk status '{status}'.", isValidation: true);
        }

        Status = status;
        ClosedAt = status == RiskStatus.Closed ? ClosedAt ?? now : null;
    }
}
