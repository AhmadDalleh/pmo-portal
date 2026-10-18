using System.Text;

namespace DataGenerator.Import;

/// <summary>Splits one CSV line (the generator never writes line breaks inside the users file).</summary>
public static class CsvLine
{
    public static List<string> Split(string line)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (quoted && ch == '"' && i + 1 < line.Length && line[i + 1] == '"')
            {
                cell.Append('"');
                i++;
            }
            else if (ch == '"')
            {
                quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else
            {
                cell.Append(ch);
            }
        }

        cells.Add(cell.ToString());
        return cells;
    }
}
