namespace FriendCore.Domain.Messaging;

/// <summary>
///     The fixed set of emoji a message may be reacted with.
/// </summary>
/// <remarks>
///     Deliberately a closed list rather than free text. A reaction is a stored, counted,
///     pushed value — an arbitrary user string here would let anyone insert a slur, a
///     lookalike glyph, or unbounded distinct values into the per-message tallies, and every
///     one of those would ride along in the realtime payload to both participants. A curated
///     set also keeps the picker finite on the client instead of shipping an emoji font.
///     Adding an emoji here is the only way to widen the set; both the validator and the
///     command handler read this, so the two can never disagree.
/// </remarks>
public static class ReactionEmoji
{
    /// <summary>Upper bound on the stored form, in UTF-16 code units (see <see cref="All" />).</summary>
    public const int MaxLength = 16;

    public const string ThumbsUp = "👍";
    public const string Heart = "❤️";
    public const string Laugh = "😂";
    public const string Sad = "😢";
    public const string Surprised = "😮";
    public const string Thanks = "🙏";
    public const string Celebrate = "🎉";

    /// <summary>Every allowed emoji, in picker order.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        ThumbsUp, Heart, Laugh, Sad, Surprised, Thanks, Celebrate
    ];

    // Declared after All on purpose: static initializers run in declaration order, so this map
    // is built from a fully populated list.
    private static readonly Dictionary<string, int> PickerOrder = All
        .Select((emoji, index) => (emoji, index))
        .ToDictionary(pair => pair.emoji, pair => pair.index, StringComparer.Ordinal);

    /// <summary>
    ///     Position in <see cref="All" />, or one past the end for anything unrecognised. Callers
    ///     use it purely as a stable tiebreak when ordering chips by count.
    /// </summary>
    public static int OrderOf(string emoji) =>
        PickerOrder.TryGetValue(emoji, out var index) ? index : All.Count;

    /// <summary>
    ///     Whether <paramref name="emoji" /> is one of <see cref="All" />. Ordinal, not
    ///     culture-aware: these are exact glyphs, and a linguistic comparison would happily
    ///     accept lookalike strings that render as something else entirely.
    /// </summary>
    public static bool IsAllowed(string? emoji) =>
        emoji is not null && All.Contains(emoji, StringComparer.Ordinal);
}