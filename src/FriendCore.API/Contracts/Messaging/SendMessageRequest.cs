namespace FriendCore.API.Contracts.Messaging;

/// <summary>
///     Text, images, or both. <c>MediaIds</c> are media items the caller already uploaded through
///     POST /media and owns — the same two-step flow posts use, so an image only has to be
///     uploaded once to be usable in either place. Order is display order.
/// </summary>
/// <param name="MediaId">
///     Deprecated single-image alias, kept so a client deployed before multi-image DMs keeps
///     working. New clients send <c>MediaIds</c>. Ignored when the same id is already in that
///     list.
/// </param>
/// <param name="ReplyToMessageId">
///     Optional. The message this replies to, scoped to the conversation server-side, so an id
///     from another thread — or one that has since been deleted — is a 404 rather than a
///     cross-thread quote.
/// </param>
public sealed record SendMessageRequest(
    string? Content,
    IReadOnlyList<string>? MediaIds = null,
    string? MediaId = null,
    string? ReplyToMessageId = null);
