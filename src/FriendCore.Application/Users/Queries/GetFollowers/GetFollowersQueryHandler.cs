using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetFollowers;

internal sealed class GetFollowersQueryHandler(
    IUserRepository userRepository,
    IFollowRepository followRepository)
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
        var users = followers.Select(u => UserSummaryResponse.FromUser(u)).ToList();
        return new FollowListResponse(users);
    }
}
