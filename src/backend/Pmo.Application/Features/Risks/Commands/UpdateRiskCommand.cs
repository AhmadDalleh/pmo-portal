namespace Pmo.Application.Features.Risks.Commands;

public record UpdateRiskCommand(
    string Type, string Title, string? Description, int OwnerId, int Probability, int Impact, string? Mitigation, DateOnly? DueDate, string Status, uint Version);
