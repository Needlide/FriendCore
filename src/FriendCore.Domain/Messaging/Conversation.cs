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
    ///     Canonical, order-independent key identifying the pair — "<c>smallerId:greaterId</c>" —
    ///     for a direct conversation. Null for anything that is not a 1:1.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Denormalised out of the participant rows purely so the database can enforce "one
    ///         direct conversation per pair". Without it, uniqueness has to be checked in code, and a
    ///         check-then-insert race lets two simultaneous "Message" taps create two threads for
    ///         the same two people — which then never merge, and leave the user with two halves of
    ///         the same conversation.
    ///     </para>
    ///     <para>
    ///         Null for group conversations rather than a sentinel, because Postgres treats NULLs as
    ///         distinct in a unique index: several groups would be allowed, and the uniqueness rule
    ///         would only ever apply to direct pairs. That is the rule we actually want.
    ///     </para>
    /// </remarks>
    public string? DirectKey { get; private set; }

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
            LastMessageAt = createdAt,
            // Sorted, so (A,B) and (B,A) cannot both exist — the race this column exists to close
            // does not care which side started it.
            DirectKey = DirectKeyFor(initiatorId, recipientId)
        };
        conversation._participants.Add(ConversationParticipant.Create(conversation.Id, initiatorId, createdAt));
        conversation._participants.Add(ConversationParticipant.Create(conversation.Id, recipientId, createdAt));
        return conversation;
    }

    /// <summary>The pair key for two users, in a form that does not depend on argument order.</summary>
    public static string DirectKeyFor(Ulid first, Ulid second) =>
        string.Join(':', first.CompareTo(second) <= 0 ? [first, second] : [second, first]);

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
