using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetFollowing;

internal sealed class GetFollowingQueryHandler(
    IUserRepository userRepository,
    IFollowRepository followRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetFollowingQuery, ErrorOr<FollowListResponse>>
{
    private const int MaxResults = 200;

    public async Task<ErrorOr<FollowListResponse>> Handle(GetFollowingQuery request, CancellationToken cancellationToken)
    {
        var slug = User.ToUsernameSlug(request.Username);
        var user = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (user is null || user.IsBanned)
            return UserErrors.NotFound;

        var following = await followRepository.GetFollowingAsync(user.Id, MaxResults, cancellationToken);

        // Same viewer-relative state as the followers list: the rows let the client restore
        // the correct Follow/Following button without an N+1 of per-row lookups.
        var followedByViewer = currentUserService.UserId is { } viewerId
            ? (await followRepository.GetFollowedIdsAsync(viewerId, cancellationToken)).ToHashSet()
            : new HashSet<Ulid>();

        var users = following
            .Select(u => UserSummaryResponse.FromUser(u, followedByViewer.Contains(u.Id)))
            .ToList();

        return new FollowListResponse(users);
    }
}
