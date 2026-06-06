namespace FriendCore.Application.Common.Models;

public sealed record UserProfileResponse(
    string UserId,
    string Username,
    string DisplayName,
    string? Bio,
    string? WebsiteUrl,
    string? Location,
    bool IsVerified,
    DateTimeOffset CreatedAt);
