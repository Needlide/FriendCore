using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

/// <param name="IsFollowedByViewer">
/// Whether the requesting user follows this profile. False for an anonymous viewer, and for
/// viewing your own profile — you are not listed as following yourself. The client needs this
/// to render the button correctly on a cold load: without it the only way to know is to have
/// clicked, so the button resets to "Follow" on every refresh.
/// </param>
public sealed record UserProfileResponse(
    string UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? WebsiteUrl,
    string? Location,
    bool IsVerified,
    bool IsFollowedByViewer,
    DateTimeOffset CreatedAt)
{
    public static UserProfileResponse FromUser(User user, bool isFollowedByViewer)
    {
        return new UserProfileResponse(
            user.Id.ToString(),
            user.Username,
            user.DisplayName,
            user.Profile.Bio,
            user.Profile.WebsiteUrl,
            user.Profile.Location,
            user.IsVerified,
            isFollowedByViewer,
            user.CreatedAt);
    }
}