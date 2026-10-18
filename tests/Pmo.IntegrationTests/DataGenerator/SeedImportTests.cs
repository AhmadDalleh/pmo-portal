using DataGenerator.Csv;
using DataGenerator.Generation;
using DataGenerator.Import;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pmo.Infrastructure.Identity;
using Pmo.Infrastructure.Persistence;
using Shouldly;
using Testcontainers.PostgreSql;

namespace Pmo.IntegrationTests.DataGenerator;

/// <summary>Own container: the import empties every table, so it must not share a database with the other tests.</summary>
public class SeedDatabaseFixture : IAsyncLifetime
{
    public const string DemoPassword = "Demo#Test-2026";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        var csv = Path.Combine(Path.GetTempPath(), "pmo-seed-" + Guid.NewGuid().ToString("N"));
        CsvExporter.WriteAll(SeedGenerator.Generate(20260930), csv);
        await CsvImporter.ImportAsync(ConnectionString, csv, DemoPassword);
        Directory.Delete(csv, true);
    }

    public PmoDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PmoDbContext>().UseNpgsql(ConnectionString).UseSnakeCaseNamingConvention().Options);

    public async Task<T> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync())!;
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}

public class SeedImportTests : IClassFixture<SeedDatabaseFixture>
{
    private readonly SeedDatabaseFixture _db;

    public SeedImportTests(SeedDatabaseFixture db)
    {
        _db = db;
    }

    [Theory]
    [InlineData("users", 18)]
    [InlineData("projects", 10)]
    [InlineData("tasks", 118)]
    [InlineData("cost_transactions", 123)]
    [InlineData("financial_forecasts", 270)]
    public async Task Import_LoadsEveryGeneratedRow(string table, int expected)
    {
        (await _db.ScalarAsync<long>($"SELECT count(*) FROM {table}")).ShouldBe(expected);
    }

    [Fact]
    public async Task Import_NearBudgetProject_HasUtilizationOf92Percent()
    {
        var utilization = await _db.ScalarAsync<decimal>(
            """
            SELECT (SELECT SUM(amount) FROM cost_transactions WHERE project_id = p.id AND status = 'posted' AND txn_date <= '2026-09-30')
                 / (SELECT initial_amount + COALESCE((SELECT SUM(amount) FROM budget_revisions
                        WHERE project_id = p.id AND status = 'approved' AND effective_date <= '2026-09-30'), 0)
                    FROM project_budgets WHERE project_id = p.id)
            FROM projects p WHERE p.code = 'PRJ-001'
            """);

        Math.Round(utilization, 2).ShouldBe(0.92m);
    }

    [Fact]
    public async Task Import_CostDatedAfterCutoff_ExistsButIsNotCountedInSpend()
    {
        var future = await _db.ScalarAsync<long>("SELECT count(*) FROM cost_transactions WHERE txn_date > '2026-09-30' AND status = 'posted'");
        var spend = await _db.ScalarAsync<decimal>(
            "SELECT SUM(amount) FROM cost_transactions WHERE project_id = (SELECT id FROM projects WHERE code = 'PRJ-009') AND status = 'posted' AND txn_date <= '2026-09-30'");

        future.ShouldBeGreaterThanOrEqualTo(1);
        spend.ShouldBe(599_500.00m);
    }

    [Fact]
    public async Task Import_ReversalNetsOutItsOriginal()
    {
        var net = await _db.ScalarAsync<decimal>(
            "SELECT SUM(o.amount + r.amount) FROM cost_transactions r JOIN cost_transactions o ON o.id = r.reverses_transaction_id");

        net.ShouldBe(0m);
    }

    [Fact]
    public async Task Import_ClosedHighExposureRisk_IsNotCountedAsOpen()
    {
        var closedHigh = await _db.ScalarAsync<long>("SELECT count(*) FROM risks WHERE status = 'closed' AND score >= 15");
        var openHigh = await _db.ScalarAsync<long>("SELECT count(*) FROM risks WHERE status IN ('open','mitigating') AND score >= 15");

        closedHigh.ShouldBeGreaterThanOrEqualTo(1);
        openHigh.ShouldBe(4);
    }

    [Fact]
    public async Task Import_DemoUsers_HaveHashedPasswordsThatVerifyAndOneRole()
    {
        await using var db = _db.CreateDbContext();
        var admin = await db.Users.SingleAsync(u => u.Email == "admin1@pmo.example");

        admin.PasswordHash.ShouldNotBe(SeedDatabaseFixture.DemoPassword);
        new PasswordHasher<AppUser>().VerifyHashedPassword(admin, admin.PasswordHash!, SeedDatabaseFixture.DemoPassword)
            .ShouldBe(PasswordVerificationResult.Success);
        (await _db.ScalarAsync<long>("SELECT count(*) FROM user_roles")).ShouldBe(18);
    }

    [Fact]
    public async Task Import_IdentitySequencesContinueAfterImportedIds()
    {
        var next = await _db.ScalarAsync<long>("SELECT nextval(pg_get_serial_sequence('projects', 'id'))");

        next.ShouldBe(11);
    }
}
