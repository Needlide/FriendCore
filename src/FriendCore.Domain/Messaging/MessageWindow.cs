namespace FriendCore.Domain.Messaging;

/// <summary>
///     A slice of a conversation centred on one message, for jumping to it from search.
/// </summary>
/// <remarks>
///     Two-way, unlike the feed, which only ever walks backwards from the newest message. Both
///     cursors are needed because the interesting message is rarely the newest one, and a reader
///     who lands on it will usually want to keep reading in whichever direction they were going.
/// </remarks>
public sealed record MessageWindow(
    IReadOnlyList<Message> Messages,
    bool HasOlder,
    bool HasNewer)
{
    /// <summary>Empty when the target is gone; null is reserved for "the query itself failed".</summary>
    public static MessageWindow Empty { get; } = new([], false, false);
}
