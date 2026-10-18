namespace Pmo.Application.Features.Risks.Dtos;

/// <summary>Score = probability x impact, computed by PostgreSQL. High exposure = score 15 or more.</summary>
public record RiskDto(
    int Id, int ProjectId, string ProjectCode, string Type, string Title, string? Description, int OwnerId, string OwnerName,
    int Probability, int Impact, int Score, bool IsHighExposure, string? Mitigation, DateOnly? DueDate, string Status,
    DateTimeOffset? ClosedAt, uint Version);
