namespace FriendCore.Domain.Posts;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    Task AddAsync(Post post, CancellationToken cancellationToken = default);

    Task UpdateAsync(CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Post post, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> GetByAuthorAsync(
        Ulid authorId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    // Posts authored by users the given user follows, newest first.
    Task<IReadOnlyList<Post>> GetHomeFeedAsync(
        Ulid userId,
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

    Task AddCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task<Comment?> GetCommentByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    Task SoftDeleteCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> GetCommentsByPostAsync(
        Ulid postId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}