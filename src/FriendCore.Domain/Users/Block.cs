namespace FriendCore.Domain.Users;

/// <summary>
///     One directional row per block (§8.1). All reads treat the relationship as bidirectional;
///     keeping the row directional preserves each side's own block if the other side unblocks.
/// </summary>
public sealed class Block
{
    private Block()
    {
    }

    public Ulid BlockerId { get; private set; }
    public Ulid BlockedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Block Create(Ulid blockerId, Ulid blockedId, DateTimeOffset createdAt)
    {
        return new Block
        {
            BlockerId = blockerId,
            BlockedId = blockedId,
            CreatedAt = createdAt
        };
    }
}
