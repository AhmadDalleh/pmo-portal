namespace Pmo.Domain.Governance;

/// <summary>Append-only activity log (DB trigger blocks UPDATE/DELETE).</summary>
public class AuditEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Null means the system or a background worker.</summary>
    public int? ActorId { get; set; }

    public int? ProjectId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }

    /// <summary>JSON (jsonb) with key fields or before/after. Never secrets.</summary>
    public string? Data { get; set; }
}
