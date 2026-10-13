namespace FriendCore.API.Contracts.Messaging;

/// <param name="Content">The replacement text. Required — editing to empty is a delete, not an edit.</param>
/// <param name="Version">
///     The <c>Message.Version</c> the client was looking at, so a rewrite based on a stale view
///     is refused with 409 instead of silently discarding the other device's save. Nullable only
///     so the controller can answer 400 with a useful message rather than a deserialization error.
/// </param>
public sealed record EditMessageRequest(string Content, int? Version = null);
