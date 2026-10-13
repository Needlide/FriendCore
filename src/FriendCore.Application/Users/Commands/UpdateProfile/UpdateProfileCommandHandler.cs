using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdateProfile;

internal sealed class UpdateProfileCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateProfileCommand, ErrorOr<UserProfileResponse>>
{
    public async Task<ErrorOr<UserProfileResponse>> Handle(
        UpdateProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return Error.Unauthorized("User.Unauthorized", "Authentication is required.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return UserErrors.NotFound;

        var updateResult = user.UpdateProfile(
            request.DisplayName,
            request.Bio,
            request.WebsiteUrl,
            request.Location,
            dateTimeProvider.UtcNow);

        if (updateResult.IsError)
            return updateResult.Errors;

        await userRepository.UpdateAsync(user, cancellationToken);

        // This is always your own profile, and you never follow yourself, so the
        // relationship is false by definition rather than by an extra query.
        return UserProfileResponse.FromUser(user, isFollowedByViewer: false);
    }
}