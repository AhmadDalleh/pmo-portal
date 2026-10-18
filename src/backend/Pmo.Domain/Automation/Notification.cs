namespace Pmo.Domain.Automation;

/// <summary>In-app notification. UNIQUE (SourceOutboxId, UserId) makes a second delivery impossible.</summary>
public class Notification
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public long SourceOutboxId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
