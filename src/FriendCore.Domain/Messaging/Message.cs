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
    public string Content { get; private set; } = null!;

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
        DateTimeOffset createdAt)
    {
        var trimmed = content?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return MessagingErrors.MessageContentRequired;

        if (trimmed.Length > MaxContentLength)
            return MessagingErrors.MessageTooLong;

        return new Message(Ulid.NewUlid(), createdAt)
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = trimmed,
            IsEncrypted = false,
            Status = MessageStatus.Sent
        };
    }
}
