using FriendCore.Domain.Moderation;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository(AppDbContext context) : IReportRepository
{
    // Tracked: step-2 resolution mutates the loaded report.
    public Task<Report?> GetByIdAsync(Ulid reportId, CancellationToken cancellationToken = default)
    {
        return context.Reports.FirstOrDefaultAsync(report => report.Id == reportId, cancellationToken);
    }

    public Task<bool> PendingExistsAsync(
        Ulid reporterId,
        ReportTargetType targetType,
        Ulid targetId,
        CancellationToken cancellationToken = default)
    {
        return context.Reports.AnyAsync(
            report => report.ReporterId == reporterId
                      && report.TargetType == targetType
                      && report.TargetId == targetId
                      && report.Status == ReportStatus.Pending,
            cancellationToken);
    }

    public async Task AddAsync(Report report, CancellationToken cancellationToken = default)
    {
        await context.Reports.AddAsync(report, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> GetQueueAsync(
        ReportStatus? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = context.Reports.AsQueryable();
        if (status is { } value)
            query = query.Where(report => report.Status == value);

        // Priority is stored as a string, so it can't be ORDER BY'd directly (alphabetical would
        // put High < Low < Medium). A CASE rank gives the intended High → Medium → Low, then
        // oldest-waiting first within a priority.
        return await query
            .OrderByDescending(report =>
                report.Priority == ReportPriority.High ? 2 :
                report.Priority == ReportPriority.Medium ? 1 : 0)
            .ThenBy(report => report.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
