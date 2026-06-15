using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.Authentication;

public sealed class RefreshTokenService(
    IRefreshTokenRepository refreshTokenRepository,
    IOptions<JwtOptions> jwtOptions,
    IDateTimeProvider dateTimeProvider)
    : IRefreshTokenService
{
    private TimeSpan Lifetime => TimeSpan.FromDays(jwtOptions.Value.RefreshTokenExpirationDays);

    public async Task<IssuedRefreshToken> IssueAsync(Ulid userId, CancellationToken cancellationToken = default)
    {
        var raw = GenerateRawToken();
        var token = RefreshToken.Create(userId, Hash(raw), dateTimeProvider.UtcNow, Lifetime);
        await refreshTokenRepository.AddAsync(token, cancellationToken);
        return new IssuedRefreshToken(raw, token.ExpiresAt);
    }

    public async Task<ErrorOr<RotatedRefreshToken>> RotateAsync(
        string rawToken,
        CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var existing = await refreshTokenRepository.GetByHashAsync(Hash(rawToken), cancellationToken);

        if (existing is null)
            return UserErrors.InvalidRefreshToken;

        // Replay of an already-revoked token signals theft — revoke the user's whole family.
        if (existing.RevokedAt is not null)
        {
            await refreshTokenRepository.RevokeAllActiveForUserAsync(existing.UserId, now, cancellationToken);
            return UserErrors.InvalidRefreshToken;
        }

        if (!existing.IsActive(now))
            return UserErrors.InvalidRefreshToken;

        existing.Revoke(now);
        var raw = GenerateRawToken();
        var replacement = RefreshToken.Create(existing.UserId, Hash(raw), now, Lifetime);
        // One save commits the revocation of the old token together with the new one.
        await refreshTokenRepository.AddAsync(replacement, cancellationToken);

        return new RotatedRefreshToken(existing.UserId, raw, replacement.ExpiresAt);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var existing = await refreshTokenRepository.GetByHashAsync(Hash(rawToken), cancellationToken);
        if (existing is null || !existing.IsActive(now))
            return;

        existing.Revoke(now);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }

    public Task RevokeAllForUserAsync(Ulid userId, CancellationToken cancellationToken = default)
    {
        return refreshTokenRepository.RevokeAllActiveForUserAsync(userId, dateTimeProvider.UtcNow, cancellationToken);
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Hash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
