using FriendCore.Domain.Discovery;
using FriendCore.Domain.Tags;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class DiscoveryRepository(AppDbContext context) : IDiscoveryRepository
{
    public async Task<IReadOnlyList<DiscoveryCandidate>> GetPeopleAsync(
        Ulid viewerId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // The viewer's own tag ids by tier (≤6 primary, ≤20 secondary) — loaded once, then fed
        // back as parameter collections so the scoring query is a single round trip. Private
        // tags participate in matching (§6.4) — they are only hidden from display, and the
        // response never reveals WHICH tags matched.
        var viewerTags = await context.Users
            .Where(user => user.Id == viewerId)
            .Select(user => new
            {
                Primary = user.ProfileTags
                    .Where(tag => tag.Tier == TagTier.Primary)
                    .Select(tag => tag.TagId)
                    .ToList(),
                Secondary = user.ProfileTags
                    .Where(tag => tag.Tier == TagTier.Secondary)
                    .Select(tag => tag.TagId)
                    .ToList()
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (viewerTags is null || (viewerTags.Primary.Count == 0 && viewerTags.Secondary.Count == 0))
            return [];

        var primary = viewerTags.Primary;
        var secondary = viewerTags.Secondary;

        // Blocked relationships are excluded automatically by the "Blocks" global query filter;
        // follows and mutes are explicit here because they are not global filters.
        var candidates = await context.Users
            .Where(user => user.Id != viewerId && !user.IsBanned)
            .Where(user => !context.Follows.Any(follow =>
                follow.FollowerId == viewerId && follow.FollowedId == user.Id))
            .Where(user => !context.Mutes.Any(mute =>
                mute.MuterId == viewerId && mute.MutedId == user.Id))
            .Select(user => new
            {
                User = user,
                Score = user.ProfileTags.Count(tag => primary.Contains(tag.TagId)) * 10
                        + user.ProfileTags.Count(tag => secondary.Contains(tag.TagId)) * 3
            })
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.User.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return candidates
            .Select(candidate => new DiscoveryCandidate(candidate.User, candidate.Score))
            .ToList();
    }
}
