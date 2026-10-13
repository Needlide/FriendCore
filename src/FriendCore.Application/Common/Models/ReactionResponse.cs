using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Common.Models;

/// <summary>
///     The result of reacting, and the shape a reaction chip renders from: the glyph, the
///     count, and whether the viewer is one of the reactors (which is what decides whether
///     the chip is drawn highlighted).
/// </summary>
public sealed record ReactionResponse(string Emoji, int Count, bool Reacted)
{
    public static ReactionResponse? FromTally(ReactionTally? tally) =>
        tally is null ? null : new ReactionResponse(tally.Emoji, tally.Count, tally.ViewerReacted);

    /// <summary>
    ///     Tallies for one message's chips. A message nobody has reacted to has no entry in the
    ///     repository's dictionary, so null is the normal input here and means "no chips".
    /// </summary>
    public static IReadOnlyList<ReactionResponse> FromTallies(IReadOnlyList<ReactionTally>? tallies) =>
        tallies is null
            ? []
            : tallies.Select(tally => new ReactionResponse(tally.Emoji, tally.Count, tally.ViewerReacted)).ToList();
}