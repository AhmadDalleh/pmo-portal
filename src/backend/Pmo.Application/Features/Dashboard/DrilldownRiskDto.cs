namespace Pmo.Application.Features.Dashboard;

public record DrilldownRiskDto(
    int Id, string ProjectCode, string Type, string Title, string OwnerName, int Probability, int Impact, int Score, string Status, DateOnly? DueDate);
