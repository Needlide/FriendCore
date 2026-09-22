using FriendCore.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace FriendCore.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository(AppDbContext context) : INotificationRepository
{
    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await context.Notifications.AddAsync(notification, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(
        Ulid recipientId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await context.Notifications
            .Where(notification => notification.RecipientId == recipientId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(Ulid recipientId, CancellationToken cancellationToken = default)
    {
        return context.Notifications.CountAsync(
            notification => notification.RecipientId == recipientId && !notification.IsRead,
            cancellationToken);
    }

    public Task<bool> ExistsUnreadAsync(
        Ulid recipientId,
        Ulid actorId,
        NotificationType type,
        CancellationToken cancellationToken = default)
    {
        return context.Notifications.AnyAsync(
            notification => notification.RecipientId == recipientId
                && notification.ActorId == actorId
                && notification.Type == type
                && !notification.IsRead,
            cancellationToken);
    }

    public Task<int> MarkAllReadAsync(Ulid recipientId, CancellationToken cancellationToken = default)
    {
        // Set-based bulk update — no entities loaded into the change tracker.
        return context.Notifications
            .Where(notification => notification.RecipientId == recipientId && !notification.IsRead)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.IsRead, true),
                cancellationToken);
    }
}
