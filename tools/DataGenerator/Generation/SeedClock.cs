namespace DataGenerator.Generation;

/// <summary>The fixed "today" of the demo data. The generator never reads the real clock, so output is reproducible.</summary>
public static class SeedClock
{
    public static readonly DateOnly AsOf = new(2026, 9, 30);

    /// <summary>A UTC timestamp on the given date.</summary>
    public static DateTimeOffset Utc(DateOnly date, int hour = 9, int minute = 0) =>
        new(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.Zero);

    /// <summary>First day of the month of the given date.</summary>
    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    public static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;

    public static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;
}
