namespace Pmo.Domain.Automation;

/// <summary>Single row (Id = 1) holding the as-of date SQL views and Power BI read.</summary>
public class ReportingSetting
{
    public short Id { get; set; }
    public DateOnly AsOfDate { get; set; }
}
