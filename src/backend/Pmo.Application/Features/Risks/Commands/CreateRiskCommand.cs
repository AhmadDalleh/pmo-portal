namespace Pmo.Application.Features.Risks.Commands;

/// <summary>D1. A score of 15 or more (probability x impact) requires a mitigation text.</summary>
public record CreateRiskCommand(
    string Type, string Title, string? Description, int OwnerId, int Probability, int Impact, string? Mitigation, DateOnly? DueDate, string Status);
