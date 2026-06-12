namespace FriendCore.Domain.Discovery;

public interface IDiscoveryRepository
{
    /// <summary>
    ///     Candidate profiles ranked by tag overlap with the viewer (§6.6): +10 per shared tag
    ///     the VIEWER holds as primary, +3 per shared tag the viewer holds as secondary —
    ///     the viewer's tiers express what matters to them; the candidate's tiers don't weigh in.
    ///     Excludes self, banned users, already-followed, muted, and (via the global query
    ///     filter) anyone in a block relationship. Returns empty when the viewer has no profile
    ///     tags — the §9.3 cold-start fallback will slot in at that branch later.
    /// </summary>
    Task<IReadOnlyList<DiscoveryCandidate>> GetPeopleAsync(
        Ulid viewerId,
        int limit,
        CancellationToken cancellationToken = default);
}
