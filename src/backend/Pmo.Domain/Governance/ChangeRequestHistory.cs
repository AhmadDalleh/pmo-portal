namespace Pmo.Domain.Governance;

/// <summary>Append-only status timeline of a change request (DB trigger blocks UPDATE/DELETE).</summary>
public class ChangeRequestHistory
{
    public long Id { get; set; }
    public int ChangeRequestId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public int ActorId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? CorrelationId { get; set; }
}
