using System.Text.Json;
using Pmo.Domain.Automation;
using Pmo.Domain.Governance;

namespace DataGenerator.Generation;

/// <summary>
/// Outbox messages, the notifications they produced, and the worker's run log (design doc §8, H1/H2).
/// One approved CR is left pending and one budget alert is left failed, so the worker and the retry button have something to do.
/// </summary>
public static class AutomationSeeder
{
    public static void Seed(SeedData data)
    {
        var messages = new List<(OutboxMessage Message, string? Title, string? Body, string? Link, int[] Recipients)>();

        var approved = data.ChangeRequests.Where(c => c.Status == ChangeRequestStatus.Approved).OrderBy(c => c.DecidedAt).ToList();
        foreach (var cr in approved)
        {
            var owner = data.Projects.Single(p => p.Id == cr.ProjectId).OwnerId;
            var isLatest = cr == approved[^1];
            messages.Add((Message("ChangeRequestApproved", $"cr-approved:{cr.Id}", new { changeRequestId = cr.Id }, cr.DecidedAt!.Value,
                    isLatest ? OutboxStatus.Pending : OutboxStatus.Processed),
                $"Change request {cr.Code} approved", $"\"{cr.Title}\" was approved.", $"/change-requests/{cr.Id}",
                new[] { cr.RequestedById, owner }.Distinct().ToArray()));
        }

        AddThreshold(data, messages, "PRJ-001", 90, new DateOnly(2026, 8, 20), OutboxStatus.Processed);
        AddThreshold(data, messages, "PRJ-009", 90, new DateOnly(2026, 7, 28), OutboxStatus.Processed);
        AddThreshold(data, messages, "PRJ-009", 100, new DateOnly(2026, 8, 19), OutboxStatus.Failed);

        var id = 1;
        foreach (var item in messages.OrderBy(m => m.Message.OccurredAt))
        {
            var message = item.Message;
            message.Id = id++;
            data.Outbox.Add(message);
            if (message.Status != OutboxStatus.Processed)
            {
                continue;
            }

            foreach (var userId in item.Recipients)
            {
                data.Notifications.Add(new Notification
                {
                    Id = data.Notifications.Count + 1, UserId = userId, Title = item.Title!, Body = item.Body!, LinkUrl = item.Link,
                    SourceOutboxId = message.Id, CreatedAt = message.ProcessedAt!.Value,
                    ReadAt = data.Notifications.Count % 3 == 0 ? message.ProcessedAt.Value.AddHours(2) : null
                });
            }
        }

        AddRuns(data);
    }

    private static void AddThreshold(SeedData data, List<(OutboxMessage, string?, string?, string?, int[])> messages,
        string code, int threshold, DateOnly day, string status)
    {
        var project = data.ProjectByCode(code);
        var title = threshold >= 100 ? $"{code} is over budget" : $"{code} has used {threshold}% of its budget";
        messages.Add((Message("BudgetThresholdCrossed", $"budget:{project.Id}:{threshold}", new { projectId = project.Id, threshold },
                SeedClock.Utc(day, 14), status),
            title, $"Posted costs crossed {threshold}% of the current approved budget.", $"/projects/{project.Id}/finance",
            [project.OwnerId]));
    }

    private static OutboxMessage Message(string type, string dedupKey, object payload, DateTimeOffset occurredAt, string status) => new()
    {
        Type = type, DedupKey = dedupKey, Payload = JsonSerializer.Serialize(payload), Status = status,
        OccurredAt = occurredAt, AvailableAt = occurredAt,
        Attempts = status == OutboxStatus.Pending ? 0 : status == OutboxStatus.Failed ? 5 : 1,
        ProcessedAt = status == OutboxStatus.Processed ? occurredAt.AddSeconds(10) : null,
        LastError = status == OutboxStatus.Failed ? "Notification channel unavailable (simulated)." : null
    };

    private static void AddRuns(SeedData data)
    {
        var runs = new List<AutomationRun>();
        foreach (var message in data.Outbox.Where(m => m.Status != OutboxStatus.Pending))
        {
            var failed = message.Status == OutboxStatus.Failed;
            runs.Add(new AutomationRun
            {
                JobName = "OutboxProcessor", StartedAt = message.OccurredAt.AddSeconds(8), FinishedAt = message.OccurredAt.AddSeconds(10),
                Status = failed ? RunStatus.Partial : RunStatus.Succeeded, MessagesProcessed = failed ? 0 : 1, MessagesFailed = failed ? 1 : 0,
                Error = message.LastError
            });
        }

        for (var day = 20; day >= 1; day--)
        {
            var started = SeedClock.Utc(SeedClock.AsOf.AddDays(-day + 1), 6);
            runs.Add(new AutomationRun
            {
                JobName = "DailyProgressSnapshot", StartedAt = started, FinishedAt = started.AddSeconds(2),
                Status = RunStatus.Succeeded, MessagesProcessed = data.Projects.Count, MessagesFailed = 0
            });
        }

        var id = 1;
        foreach (var run in runs.OrderBy(r => r.StartedAt))
        {
            run.Id = id;
            run.CorrelationId = $"seed-run-{id}";
            data.Runs.Add(run);
            id++;
        }
    }
}
