using FriendCore.Domain.Common;

namespace FriendCore.Domain.Users;

public sealed class EmailVerificationToken : Entity<Ulid>
{
    private EmailVerificationToken(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private EmailVerificationToken()
    {
    }

    public Ulid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static EmailVerificationToken Create(
        Ulid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        TimeSpan validity)
    {
        return new EmailVerificationToken(Ulid.NewUlid(), createdAt)
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