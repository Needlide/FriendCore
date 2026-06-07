using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record UserProfileResponse(
    string UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? WebsiteUrl,
    string? Location,
    bool IsVerified,
    DateTimeOffset CreatedAt)
{
    public static UserProfileResponse FromUser(User user)
    {
        return new UserProfileResponse(
            user.Id.ToString(),
            user.Username,
            user.DisplayName,
            user.Profile.Bio,
            user.Profile.WebsiteUrl,
            user.Profile.Location,
            user.IsVerified,
            user.CreatedAt);
    }
}