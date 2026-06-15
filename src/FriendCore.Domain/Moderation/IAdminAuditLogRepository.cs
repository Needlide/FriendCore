namespace FriendCore.Domain.Moderation;

public interface IAdminAuditLogRepository
{
    // Adds the entry and commits — the single SaveChanges that also flushes the tracked
    // enforcement mutations (user suspension/ban, report resolution, content removal) staged in
    // the same unit of work, so the action and its log entry land atomically.
    Task AddAndSaveAsync(AdminAuditLog entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminAuditLog>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    // Moderation history for one user (entries whose target is that user), newest first.
    Task<IReadOnlyList<AdminAuditLog>> GetForUserAsync(
        Ulid userId,
        int limit,
        CancellationToken cancellationToken = default);
}
