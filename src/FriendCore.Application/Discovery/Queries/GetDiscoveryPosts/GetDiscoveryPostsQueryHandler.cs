using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Posts.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Discovery;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Discovery.Queries.GetDiscoveryPosts;

internal sealed class GetDiscoveryPostsQueryHandler(
    ICurrentUserService currentUserService,
    IDiscoveryRepository discoveryRepository,
    IPostRepository postRepository,
    IUserRepository userRepository,
    IFollowRepository followRepository,
    IMuteRepository muteRepository,
    PostResponseAssembler responseAssembler,
    ICacheService cacheService)
    : IRequestHandler<GetDiscoveryPostsQuery, ErrorOr<DiscoveryPostsResponse>>
{
    private const int MaxResults = 20;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public async Task<ErrorOr<DiscoveryPostsResponse>> Handle(
        GetDiscoveryPostsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // Only post IDS are cached (the ranking); everything else is rehydrated live each
        // request: soft-deleted posts and blocked authors drop out of GetByIdsAsync via the
        // global query filters, and follows/mutes are re-checked below. A stale cache can
        // therefore never resurrect content the viewer must not see.
        var cachedIds = await cacheService.GetOrSetAsync(
            $"discovery:posts:{userId}",
            async token =>
            {
                var viewer = await userRepository.GetByIdAsync(userId, token);
                var preferences = await userRepository.GetPreferencesAsync(userId, token);
                var ids = await discoveryRepository.GetPostIdsAsync(
                    userId,
                    preferences?.KeywordFilters ?? [],
                    viewer?.ContentPreference ?? Domain.Users.ContentPreference.General,
                    MaxResults,
                    token);
                return ids.Select(id => id.ToString()).ToList();
            },
            CacheTtl,
            cancellationToken);

        if (cachedIds.Count == 0)
            return new DiscoveryPostsResponse([]);

        var posts = await postRepository.GetByIdsAsync(
            cachedIds.Select(Ulid.Parse).ToList(), cancellationToken);

        var followedIds = (await followRepository.GetFollowedIdsAsync(userId, cancellationToken)).ToHashSet();
        var mutedIds = (await muteRepository.GetMutedIdsAsync(userId, cancellationToken)).ToHashSet();
        var visible = posts
            .Where(post => !followedIds.Contains(post.AuthorId) && !mutedIds.Contains(post.AuthorId))
            .ToDictionary(post => post.Id);

        if (visible.Count == 0)
            return new DiscoveryPostsResponse([]);

        var ordered = cachedIds
            .Select(Ulid.Parse)
            .Where(visible.ContainsKey)
            .Select(id => visible[id])
            .ToList();

        var items = await responseAssembler.AssembleAsync(ordered, userId, cancellationToken);
        return new DiscoveryPostsResponse(items);
    }
}
