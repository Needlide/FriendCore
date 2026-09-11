using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetPreferences;

internal sealed class GetPreferencesQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository)
    : IRequestHandler<GetPreferencesQuery, ErrorOr<PreferencesResponse>>
{
    public async Task<ErrorOr<PreferencesResponse>> Handle(
        GetPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // Same read path as the update handler: ContentPreference lives on User (§5.2),
        // the accessibility toggles on UserPreferences.
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (user is null || preferences is null)
            return UserErrors.NotFound;

        return PreferencesResponse.Create(preferences, user.ContentPreference);
    }
}
