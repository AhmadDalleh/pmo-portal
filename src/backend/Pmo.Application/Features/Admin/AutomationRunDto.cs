namespace Pmo.Application.Features.Admin;

public record AutomationRunDto(
    long Id, string JobName, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, int MessagesProcessed, int MessagesFailed,
    string? Error, string? CorrelationId);
