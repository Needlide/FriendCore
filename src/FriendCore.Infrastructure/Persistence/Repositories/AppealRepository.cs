using FriendCore.Domain.Moderation;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class AppealRepository(AppDbContext context) : IAppealRepository
{
    // Tracked: review mutates the loaded appeal.
    public Task<Appeal?> GetByIdAsync(Ulid appealId, CancellationToken cancellationToken = default)
    {
        return context.Appeals.FirstOrDefaultAsync(appeal => appeal.Id == appealId, cancellationToken);
    }

    public Task<bool> ExistsForActionAsync(Ulid auditLogId, CancellationToken cancellationToken = default)
    {
        return context.Appeals.AnyAsync(appeal => appeal.AuditLogId == auditLogId, cancellationToken);
    }

    public async Task AddAsync(Appeal appeal, CancellationToken cancellationToken = default)
    {
        await context.Appeals.AddAsync(appeal, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Appeal>> GetQueueAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await context.Appeals
            .Where(appeal => appeal.Status == AppealStatus.Pending)
            .OrderBy(appeal => appeal.Deadline)
            .ThenBy(appeal => appeal.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
