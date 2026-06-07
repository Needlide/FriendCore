using FriendCore.Domain.Users;

namespace FriendCore.UnitTests.Users;

public class EmailVerificationTokenTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsExpiresAt_ToCreatedAtPlusValidity()
    {
        var token = EmailVerificationToken.Create(Ulid.NewUlid(), "hash", CreatedAt, TimeSpan.FromHours(24));

        Assert.Equal(CreatedAt.AddHours(24), token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
    }

    [Fact]
    public void IsActive_ReturnsTrue_WhenNotConsumedAndNotExpired()
    {
        var token = EmailVerificationToken.Create(Ulid.NewUlid(), "hash", CreatedAt, TimeSpan.FromHours(24));

        Assert.True(token.IsActive(CreatedAt.AddHours(1)));
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenExpired()
    {
        var token = EmailVerificationToken.Create(Ulid.NewUlid(), "hash", CreatedAt, TimeSpan.FromHours(24));

        Assert.False(token.IsActive(CreatedAt.AddHours(25)));
    }

    [Fact]
    public void IsActive_ReturnsFalse_AtExactExpiry()
    {
        var token = EmailVerificationToken.Create(Ulid.NewUlid(), "hash", CreatedAt, TimeSpan.FromHours(24));

        // ExpiresAt > at is strict, so the token is inactive at the exact expiry instant.
        Assert.False(token.IsActive(token.ExpiresAt));
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenConsumed()
    {
        var token = EmailVerificationToken.Create(Ulid.NewUlid(), "hash", CreatedAt, TimeSpan.FromHours(24));

        token.Consume(CreatedAt.AddHours(1));

        Assert.False(token.IsActive(CreatedAt.AddHours(2)));
    }

    [Fact]
    public void Consume_SetsConsumedAt()
    {
        var token = EmailVerificationToken.Create(Ulid.NewUlid(), "hash", CreatedAt, TimeSpan.FromHours(24));
        var consumedAt = CreatedAt.AddHours(3);

        token.Consume(consumedAt);

        Assert.Equal(consumedAt, token.ConsumedAt);
    }
}