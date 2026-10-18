using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pmo.Domain.Automation;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Outbox;

/// <summary>What one run did. Handy for tests and logs.</summary>
public record OutboxRunResult(int Processed, int Failed);

/// <summary>
/// One pass of the outbox worker (story H1, design doc section 8). In one transaction it locks up to 20 pending messages
/// (FOR UPDATE SKIP LOCKED, so two workers never take the same row), creates the notifications
/// (ON CONFLICT DO NOTHING, so a second delivery is impossible), marks the messages and records an automation_runs row.
/// A message that throws gets attempts + 1 and a later available_at (2, 4, 8... minutes); after 5 attempts it is failed.
/// Idle polls write no run row, otherwise the table would fill with empty rows.
/// </summary>
public sealed class OutboxBatchRunner(PmoDbContext db, NotificationBuilder builder, TimeProvider clock, ILogger<OutboxBatchRunner> logger)
{
    public const int BatchSize = 20;
    public const int MaxAttempts = 5;

    public async Task<OutboxRunResult> RunOnceAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var batch = await db.OutboxMessages
            .FromSqlInterpolated($"SELECT * FROM outbox_messages WHERE status = 'pending' AND available_at <= {now} ORDER BY id LIMIT {BatchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);
        if (batch.Count == 0)
        {
            return new OutboxRunResult(0, 0);
        }

        var run = new AutomationRun { JobName = "OutboxProcessor", StartedAt = now, Status = RunStatus.Running, CorrelationId = Guid.NewGuid().ToString("N") };
        db.AutomationRuns.Add(run);

        int processed = 0, failed = 0;
        string? lastError = null;
        foreach (var message in batch)
        {
            message.Attempts++;
            try
            {
                foreach (var n in await builder.BuildAsync(message, ct))
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        INSERT INTO notifications (user_id, title, body, link_url, source_outbox_id, created_at)
                        VALUES ({n.UserId}, {n.Title}, {n.Body}, {n.LinkUrl}, {message.Id}, {now})
                        ON CONFLICT (source_outbox_id, user_id) DO NOTHING
                        """, ct);
                }

                message.Status = OutboxStatus.Processed;
                message.ProcessedAt = now;
                message.LastError = null;
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                lastError = ex.Message;
                message.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                message.Status = message.Attempts >= MaxAttempts ? OutboxStatus.Failed : OutboxStatus.Pending;
                message.AvailableAt = now.AddMinutes(Math.Pow(2, message.Attempts));   // exponential backoff
                logger.LogWarning("Outbox message {MessageId} attempt {Attempts} failed: {Error}", message.Id, message.Attempts, ex.Message);
            }
        }

        run.FinishedAt = clock.GetUtcNow();
        run.MessagesProcessed = processed;
        run.MessagesFailed = failed;
        run.Error = lastError;
        run.Status = failed == 0 ? RunStatus.Succeeded : processed == 0 ? RunStatus.Failed : RunStatus.Partial;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Outbox run finished: {Processed} processed, {Failed} failed", processed, failed);
        return new OutboxRunResult(processed, failed);
    }
}
