using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;

namespace FriendCore.Domain.Messaging;

/// <summary>
///     Everything a reply needs to render its quote, resolved server-side because the parent
///     cannot be trusted to still be available: it may be soft-deleted, or on a page of history
///     this thread has not loaded.
/// </summary>
/// <remarks>
///     Deliberately does NOT carry the sender's name. Getting one means a user lookup on the hot
///     message-read path, and the thread does not otherwise know who the other participant is —
///     it loads messages only. The excerpt carries most of the meaning, and it is what WhatsApp
///     shows in a quote for the same reason. Worth adding later if it turns out to matter.
/// </remarks>
/// <param name="Id">The replied-to message's id. Kept so the client can anchor or highlight it.</param>
/// <param name="SenderId">Who wrote it — the client may know the id already.</param>
/// <param name="Excerpt">
///     A short preview of the text, or null when there was none (an attachment-only message).
/// </param>
/// <param name="WasDeleted">
///     True when the parent has been soft-deleted. Its text is scrubbed, so
///     <paramref name="Excerpt" /> is null in that case and the two must be told apart:
///     "attached, no text" and "gone" render differently.
/// </param>
public sealed record MessageReference(
    Ulid Id,
    Ulid SenderId,
    string? Excerpt,
    bool WasDeleted)
{
    /// <summary>
    ///     Roughly one line of preview. A quote is a reference, not a copy: rendering the whole
    ///     message would make a reply to a long message mostly a quote of that message.
    /// </summary>
    public const int MaxExcerptLength = 140;

    /// <summary>
    ///     Builds the reference from a loaded parent. Tolerates the deleted case on purpose —
    ///     see <see cref="WasDeleted" />.
    /// </summary>
    public static MessageReference From(Message parent)
    {
        var content = parent.Content;
        var excerpt = content is null
            ? null
            : content.Length <= MaxExcerptLength
                ? content
                : content[..MaxExcerptLength].TrimEnd() + "…";

        return new MessageReference(
            parent.Id,
            parent.SenderId,
            // An attachment-only parent has no text; its excerpt is null but it is NOT deleted,
            // and the client must not render that as "gone".
            excerpt,
            parent.DeletedAt is not null);
    }
}