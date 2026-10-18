using System.Net.Http.Headers;
using System.Net.Http.Json;
using DataGenerator.Csv;
using DataGenerator.Generation;
using DataGenerator.Import;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pmo.Application.Features.Auth;
using Pmo.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Pmo.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a throwaway PostgreSQL 16 container: migrations applied, then the same
/// deterministic seed data the demo uses (seed 20260930). Shared by all tests in the "api" collection.
/// Tests that change data work on rows they create themselves, so the seeded numbers stay exact for the read-only tests.
/// </summary>
public class PmoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Demo#Test-2026";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PmoDbContext>();
            await db.Database.MigrateAsync();
        }

        var csv = Path.Combine(Path.GetTempPath(), "pmo-api-seed-" + Guid.NewGuid().ToString("N"));
        CsvExporter.WriteAll(SeedGenerator.Generate(20260930), csv);
        await CsvImporter.ImportAsync(ConnectionString, csv, DemoPassword);
        Directory.Delete(csv, true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-characters-long");
        builder.UseSetting("RateLimiting:LoginPerMinute", "100000");
        builder.UseSetting("Outbox:Enabled", "false");                  // tests run the batch runner themselves
        builder.UseSetting("Automation:DailySnapshotEnabled", "false");
    }

    /// <summary>Signs in through the real endpoint and returns a client that sends the access token.</summary>
    public async Task<HttpClient> CreateClientForAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginCommand(email, DemoPassword));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        return client;
    }

    /// <summary>A fresh DbContext for arranging test data directly in the database.</summary>
    public PmoDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PmoDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PmoDbContext(options);
    }

    /// <summary>Runs a query against the test database and returns the first column of the first row.</summary>
    public async Task<T> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default! : (T)result;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
