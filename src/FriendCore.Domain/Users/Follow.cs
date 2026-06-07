namespace FriendCore.Domain.Users;

public sealed class Follow
{
    private Follow()
    {
    }

    public Ulid FollowerId { get; private set; }
    public Ulid FollowedId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Follow Create(Ulid followerId, Ulid followedId, DateTimeOffset createdAt)
    {
        return new Follow
        {
            FollowerId = followerId,
            FollowedId = followedId,
            CreatedAt = createdAt
        };
    }
}
