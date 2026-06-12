using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Discovery;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Discovery.Queries.GetDiscoveryPeople;

internal sealed class GetDiscoveryPeopleQueryHandler(
    ICurrentUserService currentUserService,
    IDiscoveryRepository discoveryRepository,
    IFollowRepository followRepository,
    IMuteRepository muteRepository,
    IBlockRepository blockRepository,
    ICacheService cacheService)
    : IRequestHandler<GetDiscoveryPeopleQuery, ErrorOr<DiscoveryPeopleResponse>>
{
    private const int MaxResults = 20;

    // §9.2 prescribes ~30-minute freshness (scheduled rebuild); cache-aside with the same TTL
    // delivers that without a scheduler dependency.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public async Task<ErrorOr<DiscoveryPeopleResponse>> Handle(
        GetDiscoveryPeopleQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var cached = await cacheService.GetOrSetAsync(
            $"discovery:people:{userId}",
            token => QueryCandidatesAsync(userId, token),
            CacheTtl,
            cancellationToken);

        // Exclusions are re-checked at serve time against live data: the scored list may be up
        // to 30 minutes stale (acceptable for ranking), but someone the viewer just blocked,
        // muted, or followed must drop out immediately — a block, in particular, is a privacy
        // guarantee that must never wait for a cache to expire. Three indexed id-lookups per
        // request keep that guarantee while the expensive scoring query stays cached.
        var excluded = await GetCurrentExclusionsAsync(userId, cancellationToken);
        var people = cached.Where(person => !excluded.Contains(person.Id)).ToList();

        return new DiscoveryPeopleResponse(people);
    }

    private async Task<List<DiscoveryPersonResponse>> QueryCandidatesAsync(
        Ulid userId,
        CancellationToken cancellationToken)
    {
        var candidates = await discoveryRepository.GetPeopleAsync(userId, MaxResults, cancellationToken);

        return candidates
            .Select(candidate => new DiscoveryPersonResponse(
                candidate.User.Id.ToString(),
                candidate.User.Username,
                candidate.User.DisplayName,
                candidate.User.Profile.Bio,
                candidate.Score))
            .ToList();
    }

    private async Task<HashSet<string>> GetCurrentExclusionsAsync(Ulid userId, CancellationToken cancellationToken)
    {
        var followed = await followRepository.GetFollowedIdsAsync(userId, cancellationToken);
        var muted = await muteRepository.GetMutedIdsAsync(userId, cancellationToken);
        var blocked = await blockRepository.GetCounterpartIdsAsync(userId, cancellationToken);

        return followed.Concat(muted).Concat(blocked)
            .Select(id => id.ToString())
            .ToHashSet();
    }
}
