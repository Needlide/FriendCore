using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetFollowers;

internal sealed class GetFollowersQueryHandler(
    IUserRepository userRepository,
    IFollowRepository followRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetFollowersQuery, ErrorOr<FollowListResponse>>
{
    private const int MaxResults = 200;

    public async Task<ErrorOr<FollowListResponse>> Handle(GetFollowersQuery request, CancellationToken cancellationToken)
    {
        var slug = User.ToUsernameSlug(request.Username);
        var user = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (user is null || user.IsBanned)
            return UserErrors.NotFound;

        var followers = await followRepository.GetFollowersAsync(user.Id, MaxResults, cancellationToken);

        // The follow relationship here belongs to the viewer, not to the profile being read:
        // without it every row renders a neutral "Follow" even for people already followed.
        var followedByViewer = currentUserService.UserId is { } viewerId
            ? (await followRepository.GetFollowedIdsAsync(viewerId, cancellationToken)).ToHashSet()
            : new HashSet<Ulid>();

        var users = followers
            .Select(u => UserSummaryResponse.FromUser(u, followedByViewer.Contains(u.Id)))
            .ToList();

        return new FollowListResponse(users);
    }
}
