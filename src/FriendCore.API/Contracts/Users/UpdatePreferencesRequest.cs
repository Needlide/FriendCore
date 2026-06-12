using FriendCore.Application.Users.Commands.UpdatePreferences;

namespace FriendCore.API.Contracts.Users;

public sealed record UpdatePreferencesRequest(
    bool HideFlashingMedia,
    bool ReduceMotion,
    bool DisableAutoplay,
    bool ReduceVisualNoise,
    UserContentPreference ContentPreference);
