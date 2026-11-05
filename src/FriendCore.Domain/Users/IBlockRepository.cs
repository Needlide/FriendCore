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

    // Ids of every user with a block relationship (either direction) with userId.
    Task<IReadOnlyList<Ulid>> GetCounterpartIdsAsync(Ulid userId, CancellationToken cancellationToken = default);

    // Ids of users this user has personally blocked. Directional on purpose: the management
    // list must never reveal a block created by someone else (§8.2), and only the blocker can
    // undo their own row.
    Task<IReadOnlyList<Ulid>> GetBlockedIdsAsync(Ulid blockerId, CancellationToken cancellationToken = default);
}
