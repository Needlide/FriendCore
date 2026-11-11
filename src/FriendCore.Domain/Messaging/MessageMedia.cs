namespace FriendCore.Domain.Messaging;

// Junction linking a message to a media item, with the order the images should appear in.
// Follows the PostMedia template: a message may carry up to four images, and a display order
// is what lets the bubble render them the way the sender picked them.
public sealed class MessageMedia
{
    private MessageMedia()
    {
    }

    public Ulid MessageId { get; private set; }
    public Ulid MediaItemId { get; private set; }
    public int DisplayOrder { get; private set; }

    public static MessageMedia Create(Ulid messageId, Ulid mediaItemId, int displayOrder)
    {
        return new MessageMedia
        {
            MessageId = messageId,
            MediaItemId = mediaItemId,
            DisplayOrder = displayOrder
        };
    }
}
