namespace FriendCore.API.Contracts.Auth;

public sealed record RegisterRequest(
    string Username,
    string DisplayName,
    string Email,
    string Password);