namespace FriendCore.Application.Common.Interfaces;

/// <summary>
///     Hands uploaded media to the asynchronous variant-processing pipeline (spec §7.1 step 5).
///     The MVP implementation is an in-process channel (the TagMaintenanceService "stand-in for
///     Hangfire" precedent); this seam is where a durable job system slots in later.
/// </summary>
public interface IMediaProcessingQueue
{
    void Enqueue(Ulid mediaId);
}
