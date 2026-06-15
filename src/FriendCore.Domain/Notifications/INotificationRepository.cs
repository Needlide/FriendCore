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

    // Marks every unread notification for the recipient as read; returns how many changed.
    Task<int> MarkAllReadAsync(Ulid recipientId, CancellationToken cancellationToken = default);
}
