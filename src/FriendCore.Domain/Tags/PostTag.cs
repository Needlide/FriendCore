namespace FriendCore.Domain.Tags;

public sealed class PostTag
{
    private PostTag()
    {
    }

    public Ulid PostId { get; private set; }
    public Ulid TagId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    internal static PostTag Create(Ulid postId, Ulid tagId, DateTimeOffset addedAt)
    {
        return new PostTag
        {
            PostId = postId,
            TagId = tagId,
            AddedAt = addedAt
        };
    }
}
