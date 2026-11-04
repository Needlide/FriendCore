using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetFollowing;

internal sealed class GetFollowingQueryHandler(
    IUserRepository userRepository,
    IFollowRepository followRepository)
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
        var users = following.Select(u => UserSummaryResponse.FromUser(u)).ToList();
        return new FollowListResponse(users);
    }
}
