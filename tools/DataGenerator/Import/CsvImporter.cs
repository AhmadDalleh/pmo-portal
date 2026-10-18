using Npgsql;

namespace DataGenerator.Import;

/// <summary>
/// Loads the CSV files into PostgreSQL with COPY, in foreign-key order (the file name is the table name).
/// The users file is special: it holds no password data, so DemoUserWriter creates those rows.
/// </summary>
public static class CsvImporter
{
    // Import order = parents before children.
    private static readonly string[] Tables =
    [
        "departments", "users", "projects", "project_members", "project_budgets", "milestones", "tasks", "progress_snapshots",
        "risks", "change_requests", "change_request_history", "audit_events", "cost_categories", "budget_revisions",
        "cost_transactions", "financial_forecasts", "outbox_messages", "automation_runs", "notifications"
    ];

    // Tables whose id column is an identity: after loading explicit ids the sequence must move past them.
    private static readonly string[] IdentityTables = Tables.Except(["project_members"]).Append("roles").ToArray();

    public static async Task ImportAsync(string connectionString, string csvDirectory, string demoPassword)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await ResetAsync(connection);
        foreach (var table in Tables)
        {
            var path = Path.Combine(csvDirectory, table + ".csv");
            if (table == "users")
            {
                await DemoUserWriter.WriteAsync(connection, path, demoPassword);
            }
            else
            {
                await CopyAsync(connection, table, path);
            }

            Console.WriteLine($"  imported {table}");
        }

        foreach (var table in IdentityTables)
        {
            await using var command = new NpgsqlCommand(
                $"SELECT setval(pg_get_serial_sequence('{table}', 'id'), COALESCE(MAX(id), 1), MAX(id) IS NOT NULL) FROM {table}",
                connection, transaction);
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    /// <summary>Empties every business and identity table (reporting_settings keeps its single row).</summary>
    private static async Task ResetAsync(NpgsqlConnection connection)
    {
        var all = string.Join(", ", Tables.Append("roles").Append("user_roles").Append("user_claims").Append("user_logins").Append("user_tokens").Append("role_claims"));
        await using var command = new NpgsqlCommand($"TRUNCATE {all} RESTART IDENTITY CASCADE", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CopyAsync(NpgsqlConnection connection, string table, string path)
    {
        var header = File.ReadLines(path).First();
        await using var writer = await connection.BeginTextImportAsync(
            $"COPY {table} ({header}) FROM STDIN (FORMAT csv, HEADER true)");
        await writer.WriteAsync(await File.ReadAllTextAsync(path));
    }
}
