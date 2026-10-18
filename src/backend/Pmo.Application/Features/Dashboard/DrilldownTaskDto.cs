namespace Pmo.Application.Features.Dashboard;

public record DrilldownTaskDto(
    int Id, string ProjectCode, string Title, string AssigneeName, string Status, string Priority, DateOnly DueDate, int DaysOverdue, int ProgressPct);
