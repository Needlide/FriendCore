namespace FriendCore.Domain.Users;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetActiveByTokenHashAsync(
        string tokenHash,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task RevokeActiveForUserAsync(Ulid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    Task UpdateAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
}
