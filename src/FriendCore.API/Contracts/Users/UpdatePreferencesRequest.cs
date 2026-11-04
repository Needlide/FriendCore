using FriendCore.Application.Users.Commands.UpdatePreferences;

namespace FriendCore.API.Contracts.Users;

public sealed record UpdatePreferencesRequest(
    bool HideFlashingMedia,
    // Non-nullable like the rest of this full-replace contract. Omitting it sends false, which
    // turns the arachnophobia warning OFF — the permissive direction. Safe only because the SPA
    // and API ship together; see the SendMessageOnEnter note below.
    bool HideArachnidMedia,
    bool ReduceMotion,
    bool DisableAutoplay,
    bool ReduceVisualNoise,
    // Non-nullable, so this endpoint stays the full replace it already is. Note the cost: a
    // client that omits it sends false and flips the user to Ctrl+Enter. Safe while the SPA and
    // API ship together; if this endpoint ever grows external clients, make it nullable and
    // coalesce to the stored value instead.
    bool SendMessageOnEnter,
    UserContentPreference ContentPreference);
