namespace Pmo.Application.Features.Dashboard;

public record OverdueByOwnerDto(int? UserId, string OwnerName, int OverdueTasks);
