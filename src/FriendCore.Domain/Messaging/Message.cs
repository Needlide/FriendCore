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
}
