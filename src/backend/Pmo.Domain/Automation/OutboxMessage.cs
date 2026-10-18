namespace Pmo.Domain.Automation;

/// <summary>An event written in the same transaction as the business change, processed later by the worker.</summary>
public class OutboxMessage
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;

    /// <summary>JSON (jsonb) with ids only.</summary>
    public string Payload { get; set; } = string.Empty;

    public string DedupKey { get; set; } = string.Empty;
    public string Status { get; set; } = OutboxStatus.Pending;
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }

    /// <summary>A pending message for the background worker. The payload holds ids only. DedupKey makes the same event impossible to queue twice.</summary>
    public static OutboxMessage Create(string type, string dedupKey, object payload, DateTimeOffset now) => new()
    {
        Type = type, DedupKey = dedupKey, Payload = System.Text.Json.JsonSerializer.Serialize(payload),
        Status = OutboxStatus.Pending, OccurredAt = now, AvailableAt = now
    };
}
