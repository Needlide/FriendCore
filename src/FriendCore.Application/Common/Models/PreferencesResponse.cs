using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record PreferencesResponse(
    bool HideFlashingMedia,
    bool ReduceMotion,
    bool DisableAutoplay,
    bool ReduceVisualNoise,
    string ContentPreference)
{
    public static PreferencesResponse Create(UserPreferences preferences, ContentPreference contentPreference) =>
        new(
            preferences.HideFlashingMedia,
            preferences.ReduceMotion,
            preferences.DisableAutoplay,
            preferences.ReduceVisualNoise,
            contentPreference.ToString());
}
