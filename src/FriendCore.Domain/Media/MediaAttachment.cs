namespace FriendCore.Domain.Media;

/// <summary>
/// Where a media item currently lives — the reverse of the attachment rule, which only ever
/// asks "is this media attached?" (see <c>IMediaRepository.GetAttachedIdsAsync</c>).
///
/// Revealing flagged media has to answer "may THIS user see it?", and that depends on what it
/// is attached to: a post is visible to whoever can see the post, a DM image only to the
/// conversation's participants. Without this, a reveal endpoint would either have to hand out
/// every flagged image to any authenticated user, or refuse all of them — the first leaks DMs,
/// the second makes the feature useless.
/// </summary>
/// <param name="PostId">Set when attached to a post.</param>
/// <param name="MessageId">Set when attached to a DM message.</param>
/// <param name="ConversationId">The message's conversation, for the participant check.</param>
public sealed record MediaAttachment(Ulid? PostId, Ulid? MessageId, Ulid? ConversationId)
{
    /// <summary>Not yet attached anywhere — a fresh upload still in a draft.</summary>
    public static MediaAttachment Unattached { get; } = new(null, null, null);
}
