using FriendCore.Domain.Common;

namespace FriendCore.Domain.Notifications;

public sealed class Notification : Entity<Ulid>
{
    private Notification(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Notification()
    {
    }

    public Ulid RecipientId { get; private set; }
    public Ulid ActorId { get; private set; }
    public NotificationType Type { get; private set; }

    // What the notification points at (§5.7) — the meaning depends on Type:
    //   NewFollower → the follower's user id; NewLike/NewComment → the post id;
    //   NewMessage → the conversation id.
    public Ulid EntityId { get; private set; }
    public bool IsRead { get; private set; }

    public static Notification Create(
        Ulid recipientId,
        Ulid actorId,
        NotificationType type,
        Ulid entityId,
        DateTimeOffset createdAt)
    {
        return new Notification(Ulid.NewUlid(), createdAt)
        {
            RecipientId = recipientId,
            ActorId = actorId,
            Type = type,
            EntityId = entityId,
            IsRead = false
        };
    }

    public void MarkRead()
    {
        IsRead = true;
    }
}
