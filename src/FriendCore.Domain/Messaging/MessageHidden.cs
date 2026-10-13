namespace FriendCore.Domain.Messaging;

/// <summary>
///     One participant's decision to remove a message from their own view. The row is untouched:
///     the other participant still has it, the report queue can still resolve against it, and the
///     attachment stays resolvable.
/// </summary>
/// <remarks>
///     <para>
///         Deliberately NOT a soft delete. <c>Message.DeletedAt</c> means "gone for both of us" and
///         scrubs the text; this means "gone for me" and preserves everything. A recipient wanting
///         an image out of their history should not thereby destroy the sender's copy of what
///         they wrote — that is irreversible, and in a group it would hand one participant power
///         over everyone else's record.
///     </para>
///     <para>
///         Per (message, user) rather than a column on the row, so it stays correct when
///         conversations gain a third participant. In a group, "hidden" means each member decides
///         for themselves, which a single nullable column could not express.
///     </para>
///     <para>
///         Hiding is private. The other participant is never told — a signal would leak that
///         somebody hid something — and nothing about it needs one, because the consent refusal
///         recorded alongside it is what actually stops the content coming back.
///     </para>
/// </remarks>
public sealed class MessageHidden
{
    private MessageHidden()
    {
    }

    public Ulid MessageId { get; private set; }

    /// <summary>The participant hiding it from themselves.</summary>
    public Ulid UserId { get; private set; }

    public DateTimeOffset HiddenAt { get; private set; }

    public static MessageHidden Create(Ulid messageId, Ulid userId, DateTimeOffset hiddenAt) =>
        new() { MessageId = messageId, UserId = userId, HiddenAt = hiddenAt };
}