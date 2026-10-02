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

    public static ErrorOr<Message> Create(
        Ulid conversationId,
        Ulid senderId,
        string? content,
        DateTimeOffset createdAt,
        Ulid? mediaItemId = null)
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
            IsEncrypted = false,
            Status = MessageStatus.Sent
        };
    }

    /// <summary>
    /// Rewrites the message text. Callers must check ownership and liveness themselves —
    /// the domain enforces only what a message can know about itself.
    /// </summary>
    public ErrorOr<Success> Edit(string content, DateTimeOffset editedAt)
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

        Content = trimmed;
        // Keeps the FIRST edit time: "edited" is a fact about the message, not a clock that
        // resets every time the sender fixes a typo.
        EditedAt ??= editedAt;
        return Result.Success;
    }

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
