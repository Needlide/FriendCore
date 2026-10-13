namespace FriendCore.Application.Common.Interfaces;

/// <summary>
///     Hands uploaded media to the asynchronous variant-processing pipeline (spec §7.1 step 5).
///     The MVP implementation is an in-process channel (the TagMaintenanceService "stand-in for
///     Hangfire" precedent); this seam is where a durable job system slots in later.
/// </summary>
public interface IMediaProcessingQueue
{
    /// <summary>
    ///     Hands an item to the pipeline, waiting for room if the queue is full.
    /// </summary>
    /// <remarks>
    ///     Awaited rather than fire-and-forget because the channel is bounded: an unbounded queue
    ///     accepts work the processor cannot keep up with and never says so, which is how a
    ///     "Processing" row becomes a mystery. Backpressure that surfaces as a slower upload beats
    ///     a queue that grows without limit. Callers pass their cancellation token so a
    ///     disconnected client releases the writer.
    /// </remarks>
    Task EnqueueAsync(Ulid mediaId, CancellationToken cancellationToken = default);
}
