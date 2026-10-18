namespace Pmo.Domain.Finance;

/// <summary>Forecast amount per project, category and month (PeriodMonth is always the 1st).</summary>
public class FinancialForecast
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int CategoryId { get; set; }
    public DateOnly PeriodMonth { get; set; }
    public decimal Amount { get; set; }
    public int UpdatedById { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
