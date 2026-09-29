namespace FriendCore.API.Contracts.Messaging;

/// <summary>
///     Text, an attachment, or both. <c>MediaId</c> is a media item the caller already
///     uploaded through POST /media and owns — the same two-step flow posts use, so an
///     image only has to be uploaded once to be usable in either place.
/// </summary>
public sealed record SendMessageRequest(string? Content, string? MediaId = null);
