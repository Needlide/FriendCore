namespace FriendCore.Domain.Posts;

// Junction linking a post to a media item, with the order the media should appear in.
public sealed class PostMedia
{
    private PostMedia()
    {
    }

    public Ulid PostId { get; private set; }
    public Ulid MediaItemId { get; private set; }
    public int DisplayOrder { get; private set; }

    public static PostMedia Create(Ulid postId, Ulid mediaItemId, int displayOrder)
    {
        return new PostMedia
        {
            PostId = postId,
            MediaItemId = mediaItemId,
            DisplayOrder = displayOrder
        };
    }
}
