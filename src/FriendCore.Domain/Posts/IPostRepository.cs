namespace FriendCore.Domain.Posts;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default);

    Task AddAsync(Post post, CancellationToken cancellationToken = default);

    Task UpdateAsync(CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Post post, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Post>> GetByAuthorAsync(
        Ulid authorId,
        DateTimeOffset? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}
