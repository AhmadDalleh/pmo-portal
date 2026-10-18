using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pmo.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Pmo.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a throwaway PostgreSQL 16 container,
/// with the InitialSchema migration applied. Shared by all tests in the "api" collection.
/// </summary>
public class PmoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PmoDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
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

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
