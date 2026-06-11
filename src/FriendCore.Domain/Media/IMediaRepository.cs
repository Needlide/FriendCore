namespace FriendCore.Domain.Media;

public interface IMediaRepository
{
    Task<MediaItem?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    // Loads the given media items (variants included). Used by CreatePost to validate ownership
    // and readiness before attaching, and by post queries to build variant URLs.
    Task<IReadOnlyList<MediaItem>> GetByIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(MediaItem media, CancellationToken cancellationToken = default);

    // Ids of items sitting in Processing since before the threshold — lost to a restart or a
    // dropped enqueue. The sweeper re-enqueues them.
    Task<IReadOnlyList<Ulid>> GetStuckProcessingIdsAsync(
        DateTimeOffset stuckBefore,
        int limit,
        CancellationToken cancellationToken = default);

    // Persists tracked changes to an already-loaded media item (e.g. after MarkReady / FlagSafety).
    Task UpdateAsync(CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(MediaItem media, CancellationToken cancellationToken = default);
}
