namespace Pmo.Infrastructure.Persistence.Configurations;

/// <summary>Builds CHECK constraint SQL from the domain's allowed-value lists so code and DB never drift.</summary>
public static class SqlCheck
{
    /// <summary>Example: In("status", ["draft", "posted"]) returns "status IN ('draft', 'posted')".</summary>
    public static string In(string column, IEnumerable<string> allowedValues)
    {
        var quoted = allowedValues.Select(v => $"'{v}'");
        return $"{column} IN ({string.Join(", ", quoted)})";
    }
}
