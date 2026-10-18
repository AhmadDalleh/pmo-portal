namespace Pmo.Application.Features.Admin;

public record OutboxMessageDto(
    long Id, string Type, string Payload, string DedupKey, string Status, DateTimeOffset OccurredAt, DateTimeOffset AvailableAt,
    int Attempts, DateTimeOffset? ProcessedAt, string? LastError);
