using DataGenerator.Csv;
using DataGenerator.Generation;
using DataGenerator.Import;
using Microsoft.EntityFrameworkCore;
using Pmo.Infrastructure.Persistence;

// Usage:
//   dotnet run --project tools/DataGenerator -- generate --seed 20260930 --out data/csv
//   dotnet run --project tools/DataGenerator -- import --csv data/csv
// import reads ConnectionStrings__Default and DEMO_USER_PASSWORD from the environment (scripts/import-data.* set them from .env).

/// <summary>Command line entry point (a named class, so it does not clash with Pmo.Api's Program in test projects).</summary>
internal static class GeneratorCli
{
    public static async Task<int> Main(string[] args)
    {
        var command = args.FirstOrDefault();
        var options = ParseOptions(args.Skip(1).ToArray());

        try
        {
            switch (command)
            {
                case "generate":
                    var seed = int.Parse(options.GetValueOrDefault("seed", "20260930"));
                    var data = SeedGenerator.Generate(seed);
                    CsvExporter.WriteAll(data, options.GetValueOrDefault("out", "data/csv"));
                    Console.WriteLine($"Generated seed {seed}: {data.Projects.Count} projects, {data.Tasks.Count} tasks, " +
                                      $"{data.Costs.Count} costs. All edge cases present.");
                    return 0;

                case "import":
                    var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                        ?? throw new InvalidOperationException("Set ConnectionStrings__Default (see .env.example).");
                    var password = Environment.GetEnvironmentVariable("DEMO_USER_PASSWORD");
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        throw new InvalidOperationException("Set DEMO_USER_PASSWORD: the password given to every demo user.");
                    }

                    var dbOptions = new DbContextOptionsBuilder<PmoDbContext>().UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options;
                    await using (var db = new PmoDbContext(dbOptions))
                    {
                        Console.WriteLine("Applying migrations...");
                        await db.Database.MigrateAsync();
                    }

                    Console.WriteLine("Importing CSV files (existing data is replaced)...");
                    await CsvImporter.ImportAsync(connectionString, options.GetValueOrDefault("csv", "data/csv"), password);
                    Console.WriteLine("Done.");
                    return 0;

                default:
                    Console.Error.WriteLine("Usage: generate [--seed N] [--out DIR] | import [--csv DIR]");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>();
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            result[args[i].TrimStart('-')] = args[i + 1];
        }

        return result;
    }
}
