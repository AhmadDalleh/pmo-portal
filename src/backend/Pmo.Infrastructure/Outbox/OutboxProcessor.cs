using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Pmo.Infrastructure.Outbox;

/// <summary>Background service: runs one outbox pass every few seconds (config Outbox:PollSeconds, default 10).</summary>
public sealed class OutboxProcessor(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<OutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Max(1, configuration.GetValue("Outbox:PollSeconds", 10));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();   // a fresh DbContext per pass
                await scope.ServiceProvider.GetRequiredService<OutboxBatchRunner>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox pass failed; the next tick will try again");
            }
        }
    }
}
