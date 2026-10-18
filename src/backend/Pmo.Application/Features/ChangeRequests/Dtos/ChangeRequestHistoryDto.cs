namespace Pmo.Application.Features.ChangeRequests.Dtos;

/// <summary>One line of the timeline (E4): who moved the request from one status to another, when and why.</summary>
public record ChangeRequestHistoryDto(long Id, string? FromStatus, string ToStatus, int ActorId, string ActorName, string? Reason, DateTimeOffset OccurredAt);
