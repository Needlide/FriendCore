using ErrorOr;

namespace FriendCore.Domain.Messaging;

/// <summary>
///     One user's emoji reaction to one message.
/// </summary>
/// <remarks>
///     No surrogate id, exactly like <c>Like</c>: the triple (message, user, emoji) is the
///     identity, carried by a composite primary key. That is what makes "react" naturally
///     idempotent — a second tap on the same emoji collides with the existing row instead of
///     stacking a duplicate, and it is why a user can hold several <em>different</em>
///     reactions on one message while never holding two of the same.
/// </remarks>
public sealed class MessageReaction
{
    private MessageReaction()
    {
    }

    public Ulid MessageId { get; private set; }

    public Ulid UserId { get; private set; }

    public string Emoji { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public static ErrorOr<MessageReaction> Create(
        Ulid messageId,
        Ulid userId,
        string emoji,
        DateTimeOffset createdAt)
    {
        // The validator already rejects this; checked again here so the invariant cannot be
        // reached by any other caller. Reactions are never soft-deleted (they carry no user
        // content worth retaining), so this type deliberately does not implement
        // ISoftDeletable and gets no global DeletedAt filter.
        if (!ReactionEmoji.IsAllowed(emoji))
            return MessagingErrors.ReactionNotAllowed;

        return new MessageReaction
        {
            MessageId = messageId,
            UserId = userId,
            Emoji = emoji,
            CreatedAt = createdAt
        };
    }
}