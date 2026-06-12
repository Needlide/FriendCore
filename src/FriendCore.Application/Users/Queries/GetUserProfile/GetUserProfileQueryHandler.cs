using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetUserProfile;

internal sealed class GetUserProfileQueryHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IBlockRepository blockRepository)
    : IRequestHandler<GetUserProfileQuery, ErrorOr<UserProfileResponse>>
{
    public async Task<ErrorOr<UserProfileResponse>> Handle(
        GetUserProfileQuery request,
        CancellationToken cancellationToken)
    {
        var slug = User.ToUsernameSlug(request.Username);
        var user = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);

        if (user is null || user.IsBanned)
            return UserErrors.NotFound;

        // §8.2: explicit block check on direct profile access — 404, never 403, so the
        // profile simply "does not exist" for either side of a block.
        if (currentUserService.UserId is { } viewerId
            && await blockRepository.ExistsBetweenAsync(viewerId, user.Id, cancellationToken))
            return UserErrors.NotFound;

        return UserProfileResponse.FromUser(user);
    }
}