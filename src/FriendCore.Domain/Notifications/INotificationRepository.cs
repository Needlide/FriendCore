namespace FriendCore.Domain.Notifications;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    // Recipient's notifications, newest first, capped at limit.
    Task<IReadOnlyList<Notification>> GetForUserAsync(
        Ulid recipientId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Ulid recipientId, CancellationToken cancellationToken = default);

    // Whether an unread notification of this type from this actor already waits —
    // drives per-sender collapsing (one ping per conversation until read).
    Task<bool> ExistsUnreadAsync(
        Ulid recipientId,
        Ulid actorId,
        NotificationType type,
        CancellationToken cancellationToken = default);

    // Marks every unread notification for the recipient as read; returns how many changed.
    Task<int> MarkAllReadAsync(Ulid recipientId, CancellationToken cancellationToken = default);

    // Recipient-scoped single load: a notification owned by someone else is not found.
    Task<Notification?> GetByIdAsync(
        Ulid id,
        Ulid recipientId,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(Notification notification, CancellationToken cancellationToken = default);

    Task DeleteAsync(Notification notification, CancellationToken cancellationToken = default);
}
