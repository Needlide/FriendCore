using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record PreferencesResponse(
    bool HideFlashingMedia,
    bool HideArachnidMedia,
    bool ReduceMotion,
    bool DisableAutoplay,
    bool ReduceVisualNoise,
    bool SendMessageOnEnter,
    string ContentPreference)
{
    public static PreferencesResponse Create(UserPreferences preferences, ContentPreference contentPreference) =>
        new(
            preferences.HideFlashingMedia,
            preferences.HideArachnidMedia,
            preferences.ReduceMotion,
            preferences.DisableAutoplay,
            preferences.ReduceVisualNoise,
            preferences.SendMessageOnEnter,
            contentPreference.ToString());
}
