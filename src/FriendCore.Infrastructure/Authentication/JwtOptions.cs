namespace FriendCore.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "FriendCore";
    public string Audience { get; init; } = "FriendCore";
    public string SecretKey { get; init; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; init; } = 60;
}