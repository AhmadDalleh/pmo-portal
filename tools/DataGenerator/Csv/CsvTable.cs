using System.Globalization;
using System.Text;

namespace DataGenerator.Csv;

/// <summary>One CSV file: the table name, the column names (identical to the database columns) and the rows.</summary>
public class CsvTable
{
    private readonly List<string?[]> _rows = [];

    public CsvTable(string name, params string[] columns)
    {
        Name = name;
        Columns = columns;
    }

    public string Name { get; }
    public string[] Columns { get; }

    public void Add(params object?[] values)
    {
        if (values.Length != Columns.Length)
        {
            throw new InvalidOperationException($"{Name}: expected {Columns.Length} values but got {values.Length}.");
        }

        _rows.Add(values.Select(Format).ToArray());
    }

    /// <summary>Plain text, LF line endings, no BOM: the same bytes on every machine.</summary>
    public string ToCsv()
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', Columns)).Append('\n');
        foreach (var row in _rows)
        {
            text.Append(string.Join(',', row.Select(Quote))).Append('\n');
        }

        return text.ToString();
    }

    // Postgres COPY reads an unquoted empty cell as NULL, so null becomes empty and empty text is not allowed.
    private static string? Format(object? value) => value switch
    {
        null => null,
        string s when s.Length == 0 => throw new InvalidOperationException("Empty strings cannot be exported; use null."),
        string s => s,
        bool b => b ? "true" : "false",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss+00", CultureInfo.InvariantCulture),
        decimal money => money.ToString("0.00", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    private static string Quote(string? cell)
    {
        if (cell is null)
        {
            return string.Empty;
        }

        return cell.IndexOfAny([',', '"', '\n', '\r']) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
    }
}
