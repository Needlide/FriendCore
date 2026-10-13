namespace FriendCore.Domain.Messaging;

public interface IMessagingRepository
{
    Task<Conversation?> GetByIdAsync(Ulid conversationId, CancellationToken cancellationToken = default);

    // The existing 1:1 conversation between the two users, if any (drives find-or-create).
    Task<Conversation?> FindDirectAsync(Ulid userA, Ulid userB, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Inserts a newly-created direct conversation, or returns the one that already exists.
    /// </summary>
    /// <remarks>
    ///     Never throws on the unique-index conflict. Callers do a find-then-insert, so two
    ///     simultaneous starts legitimately race and exactly one of them loses — the loser should
    ///     get the same conversation, not a 500. Doing the re-read here keeps that recovery in one
    ///     place rather than in every caller.
    /// </remarks>
    Task<Conversation> AddConversationOrGetExistingAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);

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
    //
    // <paramref name="source" /> decides whether the removal also records a consent refusal for
    // any image it carried — a sender's own withdrawal does not, a recipient's deletion and a
    // moderator's removal do. See MessageDeletionSource.
    Task SoftDeleteAsync(
        Message message,
        DateTimeOffset deletedAt,
        MessageDeletionSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Applies an edit as a single atomic compare-and-set: writes only if the row still
    ///     carries <paramref name="expectedVersion" />, and bumps the version in the same
    ///     statement. Returns false when the row had moved on — the caller lost the race and
    ///     should surface a conflict rather than overwrite.
    /// </summary>
    /// <remarks>
    ///     Deliberately the only write on this aggregate that is version-gated. Delete is
    ///     terminal and irreversible, so it must never be blocked because the sender was
    ///     rewording on another device; the asymmetry is the whole point of doing this as an
    ///     explicit statement rather than an EF concurrency token, which cannot be scoped to
    ///     one operation.
    /// </remarks>
    Task<bool> TryApplyEditAsync(
        Ulid conversationId,
        Ulid messageId,
        string content,
        DateTimeOffset editedAt,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    // The moderation view of a message: soft-deleted rows included, since a moderator may need
    // to action or restore one the report was filed against.
    Task<Message?> GetMessageForModerationAsync(Ulid messageId, CancellationToken cancellationToken = default);

    // Soft-delete staged for a moderator's SaveChanges — the same shape as PostRepository's, so
    // the audit write and the removal commit together (§10.3). Async because recording a consent
    // refusal needs the image's content hash; it still never saves.
    Task StageSoftDeleteAsync(
        Message message,
        DateTimeOffset deletedAt,
        MessageDeletionSource source,
        CancellationToken cancellationToken = default);

    // The user's conversations, most-recently-active first.
    Task<IReadOnlyList<Conversation>> GetConversationsForUserAsync(
        Ulid userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <param name="viewerId">
    ///     Whose view to return. Messages this participant has hidden are omitted, so the same
    ///     conversation can legitimately return different messages to each side.
    /// </param>
    Task<IReadOnlyList<Message>> GetMessagesAsync(
        Ulid conversationId,
        Ulid viewerId,
        (DateTimeOffset CreatedAt, Ulid Id)? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    // Messages in the conversation newer than lastReadAt that the user did not send themselves.
    Task<int> CountUnreadAsync(
        Ulid conversationId,
        Ulid userId,
        DateTimeOffset? lastReadAt,
        CancellationToken cancellationToken = default);

    // ---- Hiding (private, per participant) ----------------------------------------------------

    /// <summary>
    ///     Removes a message from ONE participant's view, recording a consent refusal for any
    ///     image it carried. Does not delete, scrub, or un-attach anything.
    /// </summary>
    Task HideMessageAsync(
        Ulid messageId,
        Ulid userId,
        DateTimeOffset at,
        MessageDeletionSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Messages in one conversation whose text contains <paramref name="term" />, for one
    ///     viewer. Newest first, like the feed.
    /// </summary>
    /// <remarks>
    ///     Uses a substring match rather than Postgres full-text: at the MVP's size a plain
    ///     ILIKE over a conversation is comfortably fast, and the difference is invisible until
    ///     it is not — at which point this stays a repository-private change.
    /// </remarks>
    Task<IReadOnlyList<Message>> SearchMessagesAsync(
        Ulid conversationId,
        Ulid viewerId,
        string term,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     A window of the conversation centred on one message, for jumping to it.
    /// </summary>
    /// <remarks>
    ///     Returned oldest-first, like every other message list. The cursors move outward from the
    ///     centre in both directions, which is why this is a separate shape from the one-way feed.
    /// </remarks>
    /// <returns>
    ///     <see cref="MessageWindow.Empty" /> when the target message does not exist or is not in
    ///     this conversation — never null, so callers have one way to check.
    /// </returns>
    Task<MessageWindow> GetMessagesAroundAsync(
        Ulid conversationId,
        Ulid viewerId,
        Ulid messageId,
        int radius,
        CancellationToken cancellationToken = default);

    // ---- Reply references -------------------------------------------------------------------

    /// <summary>
    ///     Loads the messages a page of replies points at, keyed by id.
    /// </summary>
    /// <remarks>
    ///     Soft-deleted parents are INCLUDED on purpose. The global filter would hide them, and a
    ///     naive lookup would then either drop the reply's quote silently or 404 the whole feed
    ///     page — so "the parent was deleted" has to be representable, which means the row has to
    ///     be visible to this query.
    /// </remarks>
    Task<IReadOnlyDictionary<Ulid, Message>> GetByIdsIncludingDeletedAsync(
        IReadOnlyCollection<Ulid> ids,
        CancellationToken cancellationToken = default);

    // ---- Reactions -------------------------------------------------------------------------
    // Counts are derived from the reaction rows on read rather than denormalized onto Message.
    // A counter column would need its own increment/decrement logic on every toggle path and
    // would drift the first time two devices raced; a grouped read over the page's ids is one
    // cheap query and cannot disagree with the rows it summarises.

    Task<bool> ReactionExistsAsync(
        Ulid messageId,
        Ulid userId,
        string emoji,
        CancellationToken cancellationToken = default);

    Task AddReactionAsync(MessageReaction reaction, CancellationToken cancellationToken = default);

    // Removes the reaction if present (returns whether it existed) and persists.
    Task<bool> RemoveReactionAsync(
        Ulid messageId,
        Ulid userId,
        string emoji,
        CancellationToken cancellationToken = default);

    // Tallies for a whole page of messages in one query, keyed by message id. Messages with no
    // reactions are simply absent from the dictionary, so callers can use TryGetValue/default.
    Task<IReadOnlyDictionary<Ulid, IReadOnlyList<ReactionTally>>> GetReactionsAsync(
        IReadOnlyCollection<Ulid> messageIds,
        Ulid viewerId,
        CancellationToken cancellationToken = default);

    // The single emoji's tally on one message, used to answer a reaction toggle without
    // reloading the page. Null when nobody has that reaction (which includes the case where
    // the toggle just removed the viewer's own and took the count to zero).
    Task<ReactionTally?> GetReactionTallyAsync(
        Ulid messageId,
        string emoji,
        Ulid viewerId,
        CancellationToken cancellationToken = default);

    // Drops every reaction on a soft-deleted message. There is no FK from reactions to
    // messages (matching the rest of the module), so without this they would outlive the
    // message as unreachable rows.
    Task RemoveReactionsForMessageAsync(Ulid messageId, CancellationToken cancellationToken = default);

    // The same cleanup staged without saving, so a moderator's reaction removal commits in the
    // same transaction as their audit entry (§10.3).
    void StageReactionRemoval(Ulid messageId);
}
