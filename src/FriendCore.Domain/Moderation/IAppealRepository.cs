namespace FriendCore.Domain.Moderation;

public interface IAppealRepository
{
    Task<Appeal?> GetByIdAsync(Ulid appealId, CancellationToken cancellationToken = default);

    // One appeal per moderation action.
    Task<bool> ExistsForActionAsync(Ulid auditLogId, CancellationToken cancellationToken = default);

    Task AddAsync(Appeal appeal, CancellationToken cancellationToken = default);

    // The single SaveChanges of an appeal review — also flushes any tracked enforcement reversal
    // (post restore / user reinstatement) staged in the same unit of work.
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // Pending appeals, soonest deadline first (§13.3 deadline tracking).
    Task<IReadOnlyList<Appeal>> GetQueueAsync(int limit, CancellationToken cancellationToken = default);

    Task<AppealMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
}
