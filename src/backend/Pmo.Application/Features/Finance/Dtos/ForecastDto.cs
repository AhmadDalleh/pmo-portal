namespace Pmo.Application.Features.Finance.Dtos;

public record ForecastDto(int CategoryId, string CategoryName, DateOnly PeriodMonth, decimal Amount);
