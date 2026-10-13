namespace FriendCore.Domain.Messaging;

/// <summary>
///     One emoji's state on one message, as seen by one viewer: how many people reacted, and
///     whether the viewer is among them. The viewer's own state rides along with the count
///     because the bubble renders a highlighted chip or a plain one from the same payload.
/// </summary>
public sealed record ReactionTally(string Emoji, int Count, bool ViewerReacted);