namespace FriendCore.Domain.Messaging;

/// <summary>
///     Junction (§5.6): one row per (conversation, user). Owned by <see cref="Conversation" />.
///     <see cref="LastReadAt" /> is how read state is tracked in the MVP — advanced by the read
///     endpoint, compared against message timestamps to derive unread counts.
/// </summary>
public sealed class ConversationParticipant
{
    private ConversationParticipant()
    {
    }

    public Ulid ConversationId { get; private set; }
    public Ulid UserId { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }
    public DateTimeOffset? LastReadAt { get; private set; }

    internal static ConversationParticipant Create(Ulid conversationId, Ulid userId, DateTimeOffset joinedAt)
    {
        return new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            JoinedAt = joinedAt
        };
    }

    // Monotonic: a stale read receipt can never roll the marker backwards.
    internal void AdvanceLastRead(DateTimeOffset at)
    {
        if (LastReadAt is null || at > LastReadAt)
            LastReadAt = at;
    }
}
