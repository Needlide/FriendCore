using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using FriendCore.Domain.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.MediaProcessing;

/// <summary>
///     Restart recovery for the in-memory queue: anything still in Processing past the threshold
///     was lost (crash, dropped enqueue) and gets re-enqueued. Re-processing a finished item is a
///     no-op, so false positives only cost wasted work. First tick fires after one interval, like
///     TagMaintenanceService.
/// </summary>
internal sealed class MediaProcessingSweeper(
    MediaProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider dateTimeProvider,
    IOptions<MediaProcessingOptions> options,
    ILogger<MediaProcessingSweeper> logger)
    : BackgroundService
{
    private const int SweepBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IMediaRepository>();

                var stuckBefore = dateTimeProvider.UtcNow.AddMinutes(-options.Value.StuckAfterMinutes);
                var stuckIds = await repository.GetStuckProcessingIdsAsync(
                    stuckBefore, SweepBatchSize, stoppingToken);

                // Awaited because the queue is bounded. The sweeper's own cancellation token is
                // passed so a shutdown mid-drain does not leave it waiting for room that will
                // never arrive; an item not re-enqueued this round is simply picked up next sweep.
                foreach (var mediaId in stuckIds)
                    await queue.EnqueueAsync(mediaId, stoppingToken);

                if (stuckIds.Count > 0)
                    logger.LogInformation("Re-enqueued {Count} media items stuck in Processing", stuckIds.Count);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down.
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Media processing sweep failed");
            }
        }
    }
}
