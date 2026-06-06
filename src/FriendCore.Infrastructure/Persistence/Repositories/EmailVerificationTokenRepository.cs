using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class EmailVerificationTokenRepository(AppDbContext context) : IEmailVerificationTokenRepository
{
    public Task<EmailVerificationToken?> GetActiveByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return context.EmailVerificationTokens
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash && token.ConsumedAt == null && token.ExpiresAt > now,
                cancellationToken);
    }

    public async Task RevokeActiveForUserAsync(
        Ulid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await context.EmailVerificationTokens
            .Where(token => token.UserId == userId && token.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.Consume(revokedAt);

        if (activeTokens.Count > 0)
            await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
    {
        await context.EmailVerificationTokens.AddAsync(token, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(EmailVerificationToken token, CancellationToken cancellationToken = default)
    {
        context.EmailVerificationTokens.Update(token);
        await context.SaveChangesAsync(cancellationToken);
    }
}
