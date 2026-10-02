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

    // Ids already linked to a post OR a message. Deliberately the UNION of both tables
    // rather than one check per aggregate: "an upload is attachable once" is a property
    // of the media item, so a per-aggregate check lets the same image go into a message
    // and then a post, which is exactly what the rule exists to prevent.
    Task<IReadOnlyList<Ulid>> GetAttachedIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default);

    // Ids of items sitting in Processing since before the threshold — lost to a restart or a
    // dropped enqueue. The sweeper re-enqueues them.
    Task<IReadOnlyList<Ulid>> GetStuckProcessingIdsAsync(
        DateTimeOffset stuckBefore,
        int limit,
        CancellationToken cancellationToken = default);

    // Persists tracked changes to an already-loaded media item (e.g. after MarkReady / FlagSafety).
    Task UpdateAsync(CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(MediaItem media, CancellationToken cancellationToken = default);

    // Moderator view: soft-deleted rows included, so a moderator can see and act on media that
    // a previous action already removed.
    Task<MediaItem?> GetByIdForModerationAsync(Ulid id, CancellationToken cancellationToken = default);

    // Soft-delete staged for a moderator's SaveChanges, matching PostRepository's shape so the
    // removal and the audit entry commit together (§10.3).
    void StageSoftDelete(MediaItem media);
}
