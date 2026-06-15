using FriendCore.Domain.Users;

namespace FriendCore.UnitTests.Users;

public class RefreshTokenTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    [Fact]
    public void Create_SetsExpiryToCreatedPlusLifetime()
    {
        var token = RefreshToken.Create(Ulid.NewUlid(), "hash", CreatedAt, Lifetime);

        Assert.Equal(CreatedAt.Add(Lifetime), token.ExpiresAt);
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public void IsActive_True_WhenNotRevokedAndNotExpired()
    {
        var token = RefreshToken.Create(Ulid.NewUlid(), "hash", CreatedAt, Lifetime);

        Assert.True(token.IsActive(CreatedAt.AddDays(1)));
    }

    [Fact]
    public void IsActive_False_WhenExpired()
    {
        var token = RefreshToken.Create(Ulid.NewUlid(), "hash", CreatedAt, Lifetime);

        Assert.False(token.IsActive(CreatedAt.Add(Lifetime).AddSeconds(1)));
    }

    [Fact]
    public void IsActive_False_WhenRevoked()
    {
        var token = RefreshToken.Create(Ulid.NewUlid(), "hash", CreatedAt, Lifetime);
        token.Revoke(CreatedAt.AddDays(1));

        Assert.False(token.IsActive(CreatedAt.AddDays(2)));
    }

    [Fact]
    public void Revoke_StampsRevokedAt()
    {
        var token = RefreshToken.Create(Ulid.NewUlid(), "hash", CreatedAt, Lifetime);
        var revokedAt = CreatedAt.AddDays(1);

        token.Revoke(revokedAt);

        Assert.Equal(revokedAt, token.RevokedAt);
    }

    [Fact]
    public void Revoke_IsIdempotent_FirstRevocationWins()
    {
        var token = RefreshToken.Create(Ulid.NewUlid(), "hash", CreatedAt, Lifetime);
        var first = CreatedAt.AddDays(1);

        token.Revoke(first);
        token.Revoke(CreatedAt.AddDays(2));

        Assert.Equal(first, token.RevokedAt);
    }
}
