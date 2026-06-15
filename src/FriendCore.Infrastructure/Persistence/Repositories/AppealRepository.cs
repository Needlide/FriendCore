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

    public async Task<AppealMetrics> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var total = await context.Appeals.CountAsync(cancellationToken);
        var pending = await context.Appeals.CountAsync(a => a.Status == AppealStatus.Pending, cancellationToken);
        var overturned = await context.Appeals.CountAsync(a => a.Status == AppealStatus.Overturned, cancellationToken);

        var reviewed = await context.Appeals
            .Where(a => a.ResolvedAt != null)
            .Select(a => new { a.CreatedAt, a.ResolvedAt })
            .ToListAsync(cancellationToken);

        var avgHours = reviewed.Count == 0
            ? (double?)null
            : reviewed.Average(a => (a.ResolvedAt!.Value - a.CreatedAt).TotalHours);

        return new AppealMetrics(total, pending, overturned, avgHours);
    }
}
