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

    // A single message, scoped to its conversation and tracked for mutation. Scoping matters:
    // a message id from another conversation must be indistinguishable from one that does not
    // exist. The soft-delete query filter means a deleted message comes back null.
    Task<Message?> GetMessageAsync(
        Ulid conversationId,
        Ulid messageId,
        CancellationToken cancellationToken = default);

    // Soft-deletes through the aggregate (which scrubs the text) rather than the generic
    // Remove-the-entity path, so the domain stays the only thing that decides delete semantics.
    Task SoftDeleteAsync(Message message, DateTimeOffset deletedAt, CancellationToken cancellationToken = default);

    // The moderation view of a message: soft-deleted rows included, since a moderator may need
    // to action or restore one the report was filed against.
    Task<Message?> GetMessageForModerationAsync(Ulid messageId, CancellationToken cancellationToken = default);

    // Soft-delete staged for a moderator's SaveChanges — the same shape as PostRepository's, so
    // the audit write and the removal commit together (§10.3).
    void StageSoftDelete(Message message, DateTimeOffset deletedAt);

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
