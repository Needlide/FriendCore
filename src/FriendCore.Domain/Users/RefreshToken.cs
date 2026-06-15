using FriendCore.Domain.Common;

namespace FriendCore.Domain.Users;

/// <summary>
///     A long-lived refresh token (§10.1). Only the SHA-256 hash is stored, never the raw value.
///     Rotated on every use: the presented token is revoked and a new one issued, so a stolen-then-
///     replayed token is detectable (it will already be revoked).
/// </summary>
public sealed class RefreshToken : Entity<Ulid>
{
    private RefreshToken(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private RefreshToken()
    {
    }

    public Ulid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static RefreshToken Create(Ulid userId, string tokenHash, DateTimeOffset createdAt, TimeSpan lifetime)
    {
        return new RefreshToken(Ulid.NewUlid(), createdAt)
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = createdAt.Add(lifetime)
        };
    }

    public bool IsActive(DateTimeOffset at) => RevokedAt is null && ExpiresAt > at;

    public void Revoke(DateTimeOffset at)
    {
        RevokedAt ??= at;
    }
}
