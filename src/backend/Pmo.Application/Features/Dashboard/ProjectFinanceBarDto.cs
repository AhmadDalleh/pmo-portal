namespace Pmo.Application.Features.Dashboard;

public record ProjectFinanceBarDto(int ProjectId, string Code, string Name, decimal CurrentBudget, decimal ActualSpend, decimal ForecastToComplete, decimal Eac);
