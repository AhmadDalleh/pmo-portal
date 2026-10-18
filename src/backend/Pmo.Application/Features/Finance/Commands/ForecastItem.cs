namespace Pmo.Application.Features.Finance.Commands;

public record ForecastItem(int CategoryId, DateOnly PeriodMonth, decimal Amount);
