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

    /// <summary>
    ///     §9.2 posts tab: ids of recent posts carrying at least one tag from the viewer's
    ///     profile tags (any tier — the posts tab is unweighted recency), authored by users the
    ///     viewer does not follow. Excludes own posts, muted authors, and keyword-filter matches;
    ///     blocked authors fall out of the global query filter. Ids only — callers rehydrate
    ///     through the normal post lookups so soft-deletes and blocks stay live even when the id
    ///     list is served from a cache.
    /// </summary>
    Task<IReadOnlyList<Ulid>> GetPostIdsAsync(
        Ulid viewerId,
        IReadOnlyList<string> keywordFilters,
        int limit,
        CancellationToken cancellationToken = default);
}
