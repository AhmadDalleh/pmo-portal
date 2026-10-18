namespace Pmo.Application.Features.Finance.Dtos;

/// <summary>One month of the trend line: posted spend up to the as-of date, forecast after it.</summary>
public record MonthlyPointDto(DateOnly Month, decimal ActualSpend, decimal Forecast);
