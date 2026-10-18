namespace Pmo.Application.Features.Finance.Dtos;

public record CategorySpendDto(int CategoryId, string Code, string Name, decimal ActualSpend, decimal ForecastToComplete);
