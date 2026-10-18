using System.Text.Json;
using Bogus;
using Pmo.Domain.Finance;
using Pmo.Domain.Governance;

namespace DataGenerator.Generation;

/// <summary>Runs every seeder in foreign-key order. Same seed in, same data out.</summary>
public static class SeedGenerator
{
    public static SeedData Generate(int seed)
    {
        Randomizer.Seed = new Random(seed);   // Bogus (names, company names) reads this global seed
        var faker = new Faker("en");
        var rng = new Rng(seed);
        var data = new SeedData();

        PeopleSeeder.Seed(data, faker);
        ProjectSeeder.Seed(data, faker);
        MilestoneSeeder.Seed(data, rng);
        TaskSeeder.Seed(data, rng);
        RiskSeeder.Seed(data, rng);
        GovernanceSeeder.Seed(data, rng);
        RevisionSeeder.Seed(data);
        AddRevisionAudit(data);
        CostSeeder.Seed(data, rng, faker);
        ForecastSeeder.Seed(data, rng);
        SnapshotSeeder.Seed(data);
        AutomationSeeder.Seed(data);
        NumberAuditEvents(data);

        EdgeCaseChecks.AssertAll(data);
        return data;
    }

    private static void AddRevisionAudit(SeedData data)
    {
        foreach (var revision in data.Revisions.Where(r => r.Status != RevisionStatus.Pending))
        {
            data.AuditEvents.Add(new AuditEvent
            {
                OccurredAt = revision.DecidedAt!.Value, ActorId = revision.DecidedById, ProjectId = revision.ProjectId,
                EntityType = "BudgetRevision", EntityId = revision.Id.ToString(), Action = $"budget_revision.{revision.Status}",
                CorrelationId = $"seed-rev-{revision.Id}",
                Data = JsonSerializer.Serialize(new { amount = revision.Amount, reason = revision.DecisionReason })
            });
        }
    }

    /// <summary>Audit ids follow time order. The governance seeder created its events with Id = 0.</summary>
    private static void NumberAuditEvents(SeedData data)
    {
        var ordered = data.AuditEvents.OrderBy(a => a.OccurredAt).ThenBy(a => a.EntityType).ThenBy(a => a.EntityId).ToList();
        data.AuditEvents.Clear();
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Id = i + 1;
            data.AuditEvents.Add(ordered[i]);
        }
    }
}
