using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Messaging;

public sealed class Message : Entity<Ulid>, ISoftDeletable
{
    public const int MaxContentLength = 5000;

    private Message(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Message()
    {
    }

    public Ulid ConversationId { get; private set; }
    public Ulid SenderId { get; private set; }

    // Nullable since DMs gained attachments. A message carries text, an attachment, or
    // both — Create enforces "at least one". A stored placeholder like "Sent a file" would
    // be indistinguishable from something the user actually typed: it would be indexed,
    // searchable, translatable, and quoted back in a report, none of which the user wrote.
    public string? Content { get; private set; }

    // At most one attachment, not a PostMedia-style junction: a chat bubble shows one
    // image, and the extra table would buy nothing until a second slot is actually
    // designed. PostMedia is the template if that day comes.
    public Ulid? MediaItemId { get; private set; }

    // MVP is plaintext at rest (§5.6); E2EE is deferred (§15.2). The column exists so the
    // contract is stable when encryption lands.
    public bool IsEncrypted { get; private set; }

    // §5.6 lifecycle. Delivered/Read transitions are a real-time concern (SignalR step / future);
    // MVP derives "read" from the recipient's ConversationParticipant.LastReadAt, so this stays
    // Sent for now.
    public MessageStatus Status { get; private set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Set the first time the sender rewrites the text; null means never edited, so the
    // client can render an "edited" marker without a separate boolean.
    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>
    ///     Bumped once per accepted edit; the value a caller must present to edit again.
    /// </summary>
    /// <remarks>
    ///     This is deliberately NOT an EF concurrency token. A configured token joins the WHERE
    ///     clause of <em>every</em> update and delete on this row, which would let a delete fail
    ///     because the sender happened to be mid-rewrite on another device — and a delete that
    ///     silently does not happen is the one genuinely harmful outcome here. Instead the edit
    ///     path is the only opt-in participant, via a compare-and-set in
    ///     <c>MessagingRepository.TryApplyEditAsync</c>, which takes the version in its WHERE
    ///     clause and bumps it in the same statement. Deletes are never gated on it.
    /// </remarks>
    public int Version { get; private set; }

    /// <summary>
    ///     The message this one replies to, or null for an ordinary message. Conversation-scoped
    ///     by validation, not by a foreign key — the same deliberate choice as the rest of this
    ///     module — so a cross-thread reply is a 404 rather than a cross-thread write.
    /// </summary>
    /// <remarks>
    ///     One level only, never a tree. A reply carries a reference, not a nested quote, which
    ///     is what stops a quote-of-a-quote from growing without bound through the read path. No
    ///     FK for the same reason <c>messages</c> has none: adding one here would be the first
    ///     referential constraint in the module, and would force a decision about deleting the
    ///     parent that the module currently sidesteps entirely.
    /// </remarks>
    public Ulid? ReplyToMessageId { get; private set; }

    public static ErrorOr<Message> Create(
        Ulid conversationId,
        Ulid senderId,
        string? content,
        DateTimeOffset createdAt,
        Ulid? mediaItemId = null,
        Ulid? replyToMessageId = null)
    {
        var trimmed = content?.Trim();
        var hasMedia = mediaItemId is not null;

        // At least one of the two, checked after trimming so "   " counts as empty.
        if (string.IsNullOrWhiteSpace(trimmed) && !hasMedia)
            return MessagingErrors.MessageEmpty;

        if (trimmed is { Length: > MaxContentLength })
            return MessagingErrors.MessageTooLong;

        return new Message(Ulid.NewUlid(), createdAt)
        {
            ConversationId = conversationId,
            SenderId = senderId,
            // Whitespace-only alongside an attachment is noise, not a caption.
            Content = string.IsNullOrWhiteSpace(trimmed) ? null : trimmed,
            MediaItemId = mediaItemId,
            ReplyToMessageId = replyToMessageId,
            IsEncrypted = false,
            Status = MessageStatus.Sent
        };
    }

    /// <summary>
    /// Rewrites the message text. Callers must check ownership and liveness themselves —
    /// the domain enforces only what a message can know about itself.
    /// </summary>
    /// <param name="content">The replacement text.</param>
    /// <param name="editedAt">When the edit was accepted.</param>
    /// <param name="expectedVersion">
    ///     The <see cref="Version" /> the caller was looking at. Null skips the check, for a
    ///     caller that has no version to present; supplying a stale one refuses the edit.
    /// </param>
    /// <remarks>
    ///     The version is compared here AND again in the UPDATE's WHERE clause. This check is
    ///     the cheap one that catches the ordinary case — a user whose other device saved a
    ///     reword minutes ago is already stale by the time this request arrives — while the
    ///     one in SQL is what closes the window between this comparison and the write.
    /// </remarks>
    public ErrorOr<Success> Edit(string content, DateTimeOffset editedAt, int? expectedVersion = null)
    {
        // A soft-deleted message is invisible to every query, so an "edit" against one
        // would silently succeed and then appear to do nothing.
        if (DeletedAt is not null)
            return MessagingErrors.MessageNotFound;

        // Refuse the whole operation for an attachment-only message rather than letting a
        // caption appear on a bubble the sender never captioned.
        if (MediaItemId is not null && Content is null)
            return MessagingErrors.MessageNotEditable;

        var trimmed = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return MessagingErrors.MessageEmpty;

        if (trimmed.Length > MaxContentLength)
            return MessagingErrors.MessageTooLong;

        // Compared last, after the request is known to be otherwise valid: a caller working
        // from a stale view should be told the conflict rather than a validation complaint
        // about text they never actually sent.
        if (expectedVersion is { } expected && expected != Version)
            return MessagingErrors.MessageChanged;

        Content = trimmed;
        // Keeps the FIRST edit time: "edited" is a fact about the message, not a clock that
        // resets every time the sender fixes a typo.
        EditedAt ??= editedAt;
        return Result.Success;
    }

    /// <summary>
    /// Advances the in-memory version to match the compare-and-set that persistence just wrote.
    /// </summary>
    /// <remarks>
    /// The edit path writes through an explicit UPDATE (see
    /// <c>MessagingRepository.TryApplyEditAsync</c>) so the version can sit in that statement's
    /// WHERE clause. EF cannot see such a write, so the aggregate would otherwise still be
    /// holding the pre-edit version — and <see cref="Version" /> is what the response and the
    /// <c>MessageEdited</c> push hand to the other participant's client for its next edit.
    /// </remarks>
    public void MarkEditCommitted() => Version++;

    /// <summary>
    /// Soft-deletes the message and scrubs its text.
    /// </summary>
    /// <remarks>
    /// The text is nulled rather than left behind for CleanupSoftDeletedContentJob: that job
    /// is declared weekly but the spec never fixes a retention window, so "eventually" is not
    /// a privacy guarantee for something a person deliberately erased. Scrubbing is what makes
    /// delete mean delete. An already-deleted message is a no-op, so a double submit is safe.
    /// </remarks>
    public void MarkDeleted(DateTimeOffset deletedAt)
    {
        if (DeletedAt is not null)
            return;

        DeletedAt = deletedAt;
        Content = null;
    }
}
