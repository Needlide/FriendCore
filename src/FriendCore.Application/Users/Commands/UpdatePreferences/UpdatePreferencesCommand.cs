using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdatePreferences;

/// <summary>Full replace of the accessibility settings and content preference (§17.2).</summary>
public sealed record UpdatePreferencesCommand(
    bool HideFlashingMedia,
    bool ReduceMotion,
    bool DisableAutoplay,
    bool ReduceVisualNoise,
    UserContentPreference ContentPreference)
    : IRequest<ErrorOr<PreferencesResponse>>;

// Application-level mirror of Domain ContentPreference so API contracts never bind Domain
// types (§2.2 layering). Member names are the wire format.
public enum UserContentPreference
{
    General,
    Sensitive,
    Explicit
}
