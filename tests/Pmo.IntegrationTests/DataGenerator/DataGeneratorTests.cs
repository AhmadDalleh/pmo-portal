using DataGenerator.Csv;
using DataGenerator.Generation;
using Pmo.Domain.Delivery;
using Shouldly;

namespace Pmo.IntegrationTests.DataGenerator;

/// <summary>Story I1 (same seed, same files) and I2 (missing edge cases fail loudly). No database needed.</summary>
public class DataGeneratorTests
{
    private const int Seed = 20260930;

    [Fact]
    public void Generate_SameSeedTwice_ProducesIdenticalCsvFiles()
    {
        var first = CsvExporter.BuildTables(SeedGenerator.Generate(Seed)).Select(t => (t.Name, t.ToCsv())).ToList();
        var second = CsvExporter.BuildTables(SeedGenerator.Generate(Seed)).Select(t => (t.Name, t.ToCsv())).ToList();

        second.ShouldBe(first);
    }

    [Fact]
    public void Generate_DifferentSeed_ProducesDifferentData()
    {
        var a = CsvExporter.BuildTables(SeedGenerator.Generate(Seed)).Single(t => t.Name == "users").ToCsv();
        var b = CsvExporter.BuildTables(SeedGenerator.Generate(Seed + 1)).Single(t => t.Name == "users").ToCsv();

        b.ShouldNotBe(a);
    }

    [Fact]
    public void Generate_MeetsVolumeTargets()
    {
        var data = SeedGenerator.Generate(Seed);

        data.Users.Count.ShouldBe(18);
        data.Projects.Count.ShouldBe(10);
        data.Tasks.Count.ShouldBeGreaterThanOrEqualTo(100);
        data.Costs.Count.ShouldBeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public void AssertAll_WhenNoOverdueTasksRemain_Throws()
    {
        var data = SeedGenerator.Generate(Seed);
        foreach (var task in data.Tasks.Where(t => t.DueDate < SeedClock.AsOf))
        {
            task.Status = ProjectTaskStatus.Done;
            task.ProgressPct = 100;
        }

        var ex = Should.Throw<InvalidOperationException>(() => EdgeCaseChecks.AssertAll(data));

        ex.Message.ShouldContain("overdue incomplete tasks");
    }

    [Fact]
    public void AssertAll_WhenFutureDatedCostRemoved_Throws()
    {
        var data = SeedGenerator.Generate(Seed);
        data.Costs.RemoveAll(c => c.TxnDate > SeedClock.AsOf);

        var ex = Should.Throw<InvalidOperationException>(() => EdgeCaseChecks.AssertAll(data));

        ex.Message.ShouldContain("dated after asOf");
    }
}
