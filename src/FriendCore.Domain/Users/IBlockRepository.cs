namespace FriendCore.Domain.Users;

public interface IBlockRepository
{
    // Directional: has blockerId blocked blockedId?
    Task<bool> ExistsAsync(Ulid blockerId, Ulid blockedId, CancellationToken cancellationToken = default);

    // Any block in either direction between the two users (§8: blocks are treated as bidirectional).
    Task<bool> ExistsBetweenAsync(Ulid userA, Ulid userB, CancellationToken cancellationToken = default);

    // Blocking severs follow relationships in both directions; the block row and the follow
    // deletions must commit atomically.
    Task AddAndRemoveFollowsAsync(Block block, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Ulid blockerId, Ulid blockedId, CancellationToken cancellationToken = default);
}
