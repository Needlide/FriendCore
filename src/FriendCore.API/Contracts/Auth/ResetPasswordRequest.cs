namespace FriendCore.API.Contracts.Auth;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
