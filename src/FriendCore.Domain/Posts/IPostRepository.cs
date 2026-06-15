namespace FriendCore.Domain.Posts;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    // Tracked, ignores block AND soft-delete filters — moderation must reach any reported post
    // (incl. one its author already deleted, to act on the author).
    Task<Post?> GetByIdForModerationAsync(Ulid id, CancellationToken cancellationToken = default);

    // Stages a soft-delete on the tracked post WITHOUT saving, so it can commit in the same
    // transaction as the moderation audit entry.
    void StageSoftDelete(Post post);

    // Order is not guaranteed; callers re-order. Soft-deleted and block-filtered posts drop out.
    Task<IReadOnlyList<Post>> GetByIdsAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(Post post, CancellationToken cancellationToken = default);

    Task UpdateAsync(CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Post post, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> GetByAuthorAsync(
        Ulid authorId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    // Posts authored by users the given user follows, newest first. Posts whose content matches
    // any keyword filter (case-insensitive substring) are excluded before the page is cut.
    Task<IReadOnlyList<Post>> GetHomeFeedAsync(
        Ulid userId,
        IReadOnlyList<string> keywordFilters,
        Users.ContentPreference contentPreference,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> LikeExistsAsync(Ulid postId, Ulid userId, CancellationToken cancellationToken = default);

    // Adds the like and persists; any tracked post count change is saved in the same unit of work.
    Task AddLikeAsync(Like like, CancellationToken cancellationToken = default);

    // Removes the like if present (returns whether it existed) and persists.
    Task<bool> RemoveLikeAsync(Ulid postId, Ulid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ulid>> GetLikedPostIdsAsync(
        Ulid userId,
        IReadOnlyCollection<Ulid> postIds,
        CancellationToken cancellationToken = default);

    // Which of the given media ids are already attached to any post (including soft-deleted ones —
    // the unique index spans them). Used by CreatePost to reject reuse with a clean error.
    Task<IReadOnlyList<Ulid>> GetAttachedMediaIdsAsync(
        IReadOnlyCollection<Ulid> mediaIds,
        CancellationToken cancellationToken = default);

    Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task<Comment?> GetCommentByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    Task SoftDeleteCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> GetCommentsByPostAsync(
        Ulid postId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}