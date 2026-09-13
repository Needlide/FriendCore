using FriendCore.Domain.Common;

namespace FriendCore.Domain.Users;

/// <summary>
///     One-shot password-reset token. Mirrors <see cref="EmailVerificationToken" /> deliberately:
///     raw token to the inbox, only its hash in the database, single use, short expiry.
/// </summary>
public sealed class PasswordResetToken : Entity<Ulid>
{
    private PasswordResetToken(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private PasswordResetToken()
    {
    }

    public Ulid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static PasswordResetToken Create(
        Ulid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        TimeSpan validity)
    {
        return new PasswordResetToken(Ulid.NewUlid(), createdAt)
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = createdAt.Add(validity)
        };
    }

    public bool IsActive(DateTimeOffset at)
    {
        return ConsumedAt is null && ExpiresAt > at;
    }

    public void Consume(DateTimeOffset at)
    {
        ConsumedAt = at;
    }
}
