using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Messaging;

public sealed class Conversation : AggregateRoot<Ulid>
{
    private readonly List<ConversationParticipant> _participants = [];

    private Conversation(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Conversation()
    {
    }

    public DateTimeOffset LastMessageAt { get; private set; }
    public IReadOnlyList<ConversationParticipant> Participants => _participants.AsReadOnly();

    /// <summary>
    ///     A 1:1 conversation between two distinct users. The participant model is group-capable
    ///     (§5.6), but the MVP only ever creates direct pairs.
    /// </summary>
    public static ErrorOr<Conversation> CreateDirect(Ulid initiatorId, Ulid recipientId, DateTimeOffset createdAt)
    {
        if (initiatorId == recipientId)
            return MessagingErrors.CannotMessageSelf;

        var conversation = new Conversation(Ulid.NewUlid(), createdAt)
        {
            LastMessageAt = createdAt
        };
        conversation._participants.Add(ConversationParticipant.Create(conversation.Id, initiatorId, createdAt));
        conversation._participants.Add(ConversationParticipant.Create(conversation.Id, recipientId, createdAt));
        return conversation;
    }

    public bool HasParticipant(Ulid userId)
    {
        return _participants.Any(participant => participant.UserId == userId);
    }

    // The other side of a 1:1 conversation, from a given participant's perspective.
    public Ulid? OtherParticipantId(Ulid userId)
    {
        return _participants.FirstOrDefault(participant => participant.UserId != userId)?.UserId;
    }

    public DateTimeOffset? LastReadAtFor(Ulid userId)
    {
        return _participants.FirstOrDefault(participant => participant.UserId == userId)?.LastReadAt;
    }

    public void RecordMessage(DateTimeOffset at)
    {
        LastMessageAt = at;
    }

    public bool MarkRead(Ulid userId, DateTimeOffset at)
    {
        var participant = _participants.FirstOrDefault(p => p.UserId == userId);
        if (participant is null)
            return false;

        participant.AdvanceLastRead(at);
        return true;
    }
}
