using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pmo.Domain.Automation;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Outbox;

/// <summary>One notification to create for one user.</summary>
public record NotificationDraft(int UserId, string Title, string Body, string? LinkUrl);

/// <summary>
/// Turns an outbox message into notifications. The payload holds ids only; names and texts are read fresh from the database.
/// An unknown message type or a missing row throws, so the batch runner counts an attempt and retries later.
/// </summary>
public sealed class NotificationBuilder(PmoDbContext db)
{
    public async Task<List<NotificationDraft>> BuildAsync(OutboxMessage message, CancellationToken ct)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        return message.Type switch
        {
            "ChangeRequestApproved" => await ChangeRequestApprovedAsync(payload.RootElement.GetProperty("changeRequestId").GetInt32(), ct),
            "BudgetThresholdCrossed" => await BudgetThresholdAsync(
                payload.RootElement.GetProperty("projectId").GetInt32(), payload.RootElement.GetProperty("threshold").GetInt32(), ct),
            _ => throw new InvalidOperationException($"No handler for outbox message type '{message.Type}'.")
        };
    }

    /// <summary>H1. The requester and the project owner are told (once each, even if they are the same person).</summary>
    private async Task<List<NotificationDraft>> ChangeRequestApprovedAsync(int changeRequestId, CancellationToken ct)
    {
        var cr = await db.ChangeRequests.AsNoTracking().FirstOrDefaultAsync(c => c.Id == changeRequestId, ct)
                 ?? throw new InvalidOperationException($"Change request {changeRequestId} no longer exists.");
        var owner = await db.Projects.AsNoTracking().Where(p => p.Id == cr.ProjectId).Select(p => p.OwnerId).FirstAsync(ct);

        return new[] { cr.RequestedById, owner }.Distinct()
            .Select(userId => new NotificationDraft(userId, $"Change request {cr.Code} approved", $"\"{cr.Title}\" was approved.", $"/change-requests/{cr.Id}"))
            .ToList();
    }

    /// <summary>H2. The project owner is told that spend crossed 90% of the budget, or the budget itself.</summary>
    private async Task<List<NotificationDraft>> BudgetThresholdAsync(int projectId, int threshold, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
                      ?? throw new InvalidOperationException($"Project {projectId} no longer exists.");
        var title = threshold >= 100 ? $"{project.Code} is over budget" : $"{project.Code} has used {threshold}% of its budget";
        return [new NotificationDraft(project.OwnerId, title, $"Posted costs crossed {threshold}% of the current approved budget.", $"/projects/{project.Id}/finance")];
    }
}
