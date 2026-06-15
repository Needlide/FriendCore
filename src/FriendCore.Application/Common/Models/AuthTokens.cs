namespace FriendCore.Application.Common.Models;

/// <summary>
///     The full result of an authentication: the body payload (<see cref="Auth" />, returned to the
///     client) plus the raw refresh token, which the API writes into an HttpOnly cookie and never
///     includes in the response body.
/// </summary>
public sealed record AuthTokens(
    AuthResponse Auth,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
