using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FriendCore.Infrastructure.MediaProcessing;

/// <summary>Single consumer of the media processing channel; one scope per job.</summary>
internal sealed class MediaProcessingService(
    MediaProcessingQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<MediaProcessingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mediaId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ProcessMediaJob>()
                    .ProcessAsync(mediaId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Host is shutting down; the sweeper recovers the in-flight item after restart.
                return;
            }
            catch (Exception exception)
            {
                // ProcessMediaJob handles its own failures; this guards the loop itself (e.g. a
                // scope resolution error) so one bad item cannot kill the consumer.
                logger.LogError(exception, "Media processing loop error for {MediaId}", mediaId);
            }
        }
    }
}
