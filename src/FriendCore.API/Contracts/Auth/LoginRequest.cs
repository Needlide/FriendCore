namespace FriendCore.API.Contracts.Auth;

public sealed record LoginRequest(string Email, string Password);