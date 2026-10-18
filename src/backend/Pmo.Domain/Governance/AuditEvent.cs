using System.Text.Json;

namespace Pmo.Domain.Governance;

/// <summary>Append-only activity log (database trigger blocks UPDATE and DELETE).</summary>
public class AuditEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>NULL = system or background worker.</summary>
    public int? ActorId { get; set; }

    public int? ProjectId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }

    /// <summary>jsonb: key facts or before/after. Never secrets.</summary>
    public string? Data { get; set; }

    /// <summary>Builds an event; <paramref name="data"/> is serialised to JSON.</summary>
    public static AuditEvent Create(string action, string entityType, object entityId, int? actorId, int? projectId,
        string? correlationId, DateTimeOffset now, object? data = null) => new()
    {
        OccurredAt = now, ActorId = actorId, ProjectId = projectId, EntityType = entityType,
        EntityId = entityId.ToString()!, Action = action, CorrelationId = correlationId,
        Data = data is null ? null : JsonSerializer.Serialize(data)
    };
}
