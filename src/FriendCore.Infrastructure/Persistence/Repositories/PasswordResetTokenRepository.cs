using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class PasswordResetTokenRepository(AppDbContext context) : IPasswordResetTokenRepository
{
    public Task<PasswordResetToken?> GetActiveByTokenHashAsync(
        string tokenHash,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        return context.PasswordResetTokens
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash && token.ConsumedAt == null && token.ExpiresAt > asOf,
                cancellationToken);
    }

    public async Task RevokeActiveForUserAsync(
        Ulid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await context.PasswordResetTokens
            .Where(token => token.UserId == userId && token.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.Consume(revokedAt);

        if (activeTokens.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        await context.PasswordResetTokens.AddAsync(token, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        context.PasswordResetTokens.Update(token);
        await context.SaveChangesAsync(cancellationToken);
    }
}
