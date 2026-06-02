using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Interfaces;

public interface IJwtTokenService
{
    AuthTokenResult GenerateAccessToken(User user);
}

public sealed record AuthTokenResult(string AccessToken, DateTimeOffset ExpiresAt);