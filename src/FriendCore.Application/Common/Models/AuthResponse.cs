namespace FriendCore.Application.Common.Models;

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string UserId,
    string Username,
    string DisplayName,
    bool IsEmailVerified);