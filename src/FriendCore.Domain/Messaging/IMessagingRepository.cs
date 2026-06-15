namespace FriendCore.Domain.Messaging;

public interface IMessagingRepository
{
    Task<Conversation?> GetByIdAsync(Ulid conversationId, CancellationToken cancellationToken = default);

    // The existing 1:1 conversation between the two users, if any (drives find-or-create).
    Task<Conversation?> FindDirectAsync(Ulid userA, Ulid userB, CancellationToken cancellationToken = default);

    Task AddConversationAsync(Conversation conversation, CancellationToken cancellationToken = default);

    // Persists tracked mutations (RecordMessage, MarkRead) over the shared context.
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task AddMessageAsync(Message message, CancellationToken cancellationToken = default);

    // The user's conversations, most-recently-active first.
    Task<IReadOnlyList<Conversation>> GetConversationsForUserAsync(
        Ulid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetMessagesAsync(
        Ulid conversationId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    // Messages in the conversation newer than lastReadAt that the user did not send themselves.
    Task<int> CountUnreadAsync(
        Ulid conversationId,
        Ulid userId,
        DateTimeOffset? lastReadAt,
        CancellationToken cancellationToken = default);
}
