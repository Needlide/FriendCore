namespace FriendCore.Domain.Moderation;

public interface IReportRepository
{
    Task<Report?> GetByIdAsync(Ulid reportId, CancellationToken cancellationToken = default);

    // Dedup: one pending report per reporter per target keeps the queue from flooding.
    Task<bool> PendingExistsAsync(
        Ulid reporterId,
        ReportTargetType targetType,
        Ulid targetId,
        CancellationToken cancellationToken = default);

    Task AddAsync(Report report, CancellationToken cancellationToken = default);

    // Moderation queue: highest priority first, oldest-waiting first within a priority.
    // Optionally filtered by status (null = all).
    Task<IReadOnlyList<Report>> GetQueueAsync(
        ReportStatus? status,
        int limit,
        CancellationToken cancellationToken = default);
}
