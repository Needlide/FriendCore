namespace FriendCore.Domain.Common;

public abstract class Entity<TId> where TId : notnull
{
    protected Entity(TId id, DateTimeOffset createdAt)
    {
        Id = id;
        CreatedAt = createdAt;
    }

    protected Entity()
    {
    }

    public TId Id { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
}