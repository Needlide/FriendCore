namespace FriendCore.Domain.Posts;

public sealed class Like
{
    private Like()
    {
    }

    public Ulid PostId { get; private set; }
    public Ulid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Like Create(Ulid postId, Ulid userId, DateTimeOffset createdAt)
    {
        return new Like
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = createdAt
        };
    }
}
