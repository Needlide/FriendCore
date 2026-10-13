namespace FriendCore.Domain.Media;

/// <summary>
///     A record that one participant will not accept a given image from another, created when
///     the recipient deletes a message carrying it (or a moderator removes one).
/// </summary>
/// <remarks>
///     <para>
///         This is a consent record, not an anti-spam counter, and the difference is the whole
///         design. A counter is a guess about intent; this is a person declining something. So
///         there are no false positives, it cannot be tripped by accident, and it means exactly
///         what it says: "not you, not this file".
///     </para>
///     <para>
///         Keyed on the CONTENT hash, so it is scoped to the exact bytes and survives a
///         re-save or a re-crop — a different file is a different matter and is not refused. Keyed
///         on the pair as well, so a refusal blocks that one sender to that one recipient and
///         nobody else: the file stays perfectly sendable to anyone else.
///     </para>
///     <para>
///         Deliberately NOT recorded when the sender withdraws their own message. That is A
///         declining to send something, not B declining to receive it — nobody's wishes were
///         overridden, so nothing needs protecting, and recording it would permanently stop A
///         re-sharing an identical image with B later, which is pure friction and confusing.
///         The abuse case this feature exists for — B deletes, A keeps resending — is covered by
///         B's deletion on the first pass regardless.
///     </para>
/// </remarks>
public sealed class MediaRefusal
{
    private MediaRefusal()
    {
    }

    /// <summary>SHA-256 of the refused upload's bytes. See <c>MediaItem.ContentHash</c>.</summary>
    public string ContentHash { get; private set; } = default!;

    /// <summary>The participant who declined it. They keep being able to receive other images.</summary>
    public Ulid RecipientId { get; private set; }

    /// <summary>The participant whose image was declined. Scoped to them, not blocked globally.</summary>
    public Ulid SenderId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MediaRefusal Create(
        string contentHash,
        Ulid recipientId,
        Ulid senderId,
        DateTimeOffset createdAt) =>
        new()
        {
            ContentHash = contentHash,
            RecipientId = recipientId,
            SenderId = senderId,
            CreatedAt = createdAt
        };
}