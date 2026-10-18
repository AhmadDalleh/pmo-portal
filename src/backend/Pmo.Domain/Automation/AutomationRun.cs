namespace Pmo.Domain.Automation;

/// <summary>One execution of a background job, for the automation log.</summary>
public class AutomationRun
{
    public long Id { get; set; }
    public string JobName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = RunStatus.Running;
    public int MessagesProcessed { get; set; }
    public int MessagesFailed { get; set; }
    public string? Error { get; set; }
    public string? CorrelationId { get; set; }
}
