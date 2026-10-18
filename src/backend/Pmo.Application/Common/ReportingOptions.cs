namespace Pmo.Application.Common;

/// <summary>Config section "Reporting". All KPIs are calculated "as of" a date; the demo default is 2026-09-30.</summary>
public class ReportingOptions
{
    public const string SectionName = "Reporting";

    public DateOnly DefaultAsOf { get; set; } = new(2026, 9, 30);

    /// <summary>The date from the request when given, otherwise the configured default.</summary>
    public DateOnly Resolve(DateOnly? asOf) => asOf ?? DefaultAsOf;
}
