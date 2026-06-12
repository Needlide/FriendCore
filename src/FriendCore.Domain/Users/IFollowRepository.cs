namespace FriendCore.Domain.Users;

public interface IFollowRepository
{
    Task<bool> ExistsAsync(Ulid followerId, Ulid followedId, CancellationToken cancellationToken = default);

    // A follow in either direction between the two users.
    Task<bool> ExistsBetweenAsync(Ulid userA, Ulid userB, CancellationToken cancellationToken = default);

    // Ids of every user that userId follows.
    Task<IReadOnlyList<Ulid>> GetFollowedIdsAsync(Ulid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Follow follow, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Ulid followerId, Ulid followedId, CancellationToken cancellationToken = default);

    // Users who follow userId.
    Task<IReadOnlyList<User>> GetFollowersAsync(Ulid userId, int limit, CancellationToken cancellationToken = default);

    // Users that userId follows.
    Task<IReadOnlyList<User>> GetFollowingAsync(Ulid userId, int limit, CancellationToken cancellationToken = default);
}
