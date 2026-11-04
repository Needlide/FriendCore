using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdatePreferences;

internal sealed class UpdatePreferencesCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdatePreferencesCommand, ErrorOr<PreferencesResponse>>
{
    public async Task<ErrorOr<PreferencesResponse>> Handle(
        UpdatePreferencesCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        var preferences = await userRepository.GetPreferencesAsync(userId, cancellationToken);
        if (user is null || preferences is null)
            return UserErrors.NotFound;

        var now = dateTimeProvider.UtcNow;

        // ContentPreference lives on User (§5.2); the accessibility toggles on UserPreferences.
        user.SetContentPreference(MapContentPreference(request.ContentPreference), now);
        preferences.UpdateAccessibility(
            request.HideFlashingMedia,
            request.HideArachnidMedia,
            request.ReduceMotion,
            request.DisableAutoplay,
            request.ReduceVisualNoise,
            now);
        preferences.UpdateComposer(request.SendMessageOnEnter, now);

        await userRepository.UpdateAsync(user, cancellationToken);
        return PreferencesResponse.Create(preferences, user.ContentPreference);
    }

    // Out-of-range values are rejected by the validator's IsInEnum before the handler runs.
    private static ContentPreference MapContentPreference(UserContentPreference preference) =>
        preference switch
        {
            UserContentPreference.General => ContentPreference.General,
            UserContentPreference.Sensitive => ContentPreference.Sensitive,
            UserContentPreference.Explicit => ContentPreference.Explicit,
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, null)
        };
}
