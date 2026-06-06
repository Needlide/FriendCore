using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Users.Commands.UpdateProfile;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetUserProfile;

internal sealed class GetUserProfileQueryHandler(IUserRepository userRepository)
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

        return UpdateProfileCommandHandler.MapProfile(user);
    }
}
