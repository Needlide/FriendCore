namespace FriendCore.API.Contracts.Messaging;

/// <summary>
///     Adds the caller's reaction, or takes it back if they already hold it. The emoji travels
///     in the body rather than the route because a glyph in a path segment would arrive
///     percent-encoded, and every proxy between client and server gets a vote on whether it
///   decodes cleanly.
/// </summary>
public sealed record ToggleReactionRequest(string Emoji);