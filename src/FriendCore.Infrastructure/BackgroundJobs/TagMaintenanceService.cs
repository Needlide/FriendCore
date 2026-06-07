using FriendCore.Application.Tags.Commands.ArchiveStaleTags;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FriendCore.Infrastructure.BackgroundJobs;

// MVP stand-in for the Hangfire job in §21: periodically auto-archives stale tags.
// The first tick fires after the interval, so it never runs at host startup.
public sealed class TagMaintenanceService(
    IServiceScopeFactory scopeFactory,
    ILogger<TagMaintenanceService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(new ArchiveStaleTagsCommand(), stoppingToken);

                if (!result.IsError && result.Value.ArchivedCount > 0)
                    logger.LogInformation("Auto-archived {Count} stale tags", result.Value.ArchivedCount);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Tag auto-archive sweep failed");
            }
        }
    }
}
