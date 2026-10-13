namespace FriendCore.Application.Common.Models;

/// <summary>
///     The quoted-parent reference carried on a reply. Wire form of the domain
///     <c>MessageReference</c>: ids are strings here, as everywhere in this payload family, so
///     the API never binds a domain type or serializes a raw <c>Ulid</c>.
/// </summary>
/// <param name="Id">The replied-to message's id, so the client can anchor or highlight it.</param>
/// <param name="SenderId">Who wrote it. No name — that would cost a user lookup on the hot read path.</param>
/// <param name="Excerpt">
///     A short preview, or null when the parent had no text — either because it was an
///     attachment-only message or because it has been deleted. <paramref name="WasDeleted" />
///     tells the two apart, because they render differently.
/// </param>
/// <param name="WasDeleted">True when the parent has been soft-deleted and its text scrubbed.</param>
public sealed record MessageReferenceResponse(
    string Id,
    string SenderId,
    string? Excerpt,
    bool WasDeleted);