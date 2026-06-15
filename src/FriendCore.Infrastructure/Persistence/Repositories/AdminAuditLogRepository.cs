using FriendCore.Domain.Moderation;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class AdminAuditLogRepository(AppDbContext context) : IAdminAuditLogRepository
{
    public async Task AddAndSaveAsync(AdminAuditLog entry, CancellationToken cancellationToken = default)
    {
        await context.AdminAuditLog.AddAsync(entry, cancellationToken);
        // The single SaveChanges of a moderation action: flushes the audit entry together with
        // any tracked enforcement mutations (user suspension/ban, report resolution, staged
        // content soft-delete) so they commit atomically.
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<AdminAuditLog?> GetByIdAsync(Ulid id, CancellationToken cancellationToken = default)
    {
        return context.AdminAuditLog
            .AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminAuditLog>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.AdminAuditLog
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdminAuditLog>> GetForUserAsync(
        Ulid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.AdminAuditLog
            .Where(entry => entry.TargetType == ReportTargetType.User && entry.TargetId == userId)
            .OrderByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
