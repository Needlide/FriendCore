namespace FriendCore.API.Contracts.Messaging;

/// <summary>
///     Text, an attachment, or both. <c>MediaId</c> is a media item the caller already
///     uploaded through POST /media and owns — the same two-step flow posts use, so an
///     image only has to be uploaded once to be usable in either place.
/// </summary>
/// <param name="ReplyToMessageId">
///     Optional. The message this replies to, scoped to the conversation server-side, so an id
///     from another thread — or one that has since been deleted — is a 404 rather than a
///     cross-thread quote.
/// </param>
public sealed record SendMessageRequest(
    string? Content,
    string? MediaId = null,
    string? ReplyToMessageId = null);
