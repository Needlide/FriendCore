namespace FriendCore.API.Contracts.Users;

public sealed record UpdateProfileRequest(
    string? DisplayName,
    string? Bio,
    string? WebsiteUrl,
    string? Location);
