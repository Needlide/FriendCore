namespace FriendCore.Domain.Users;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> GetActiveByTokenHashAsync(
        string tokenHash,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task RevokeActiveForUserAsync(Ulid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task AddAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);

    Task UpdateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default);
}