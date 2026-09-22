using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Notifications;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Logging;

namespace FriendCore.Application.Notifications.Services;

internal sealed class NotificationService(
    INotificationRepository notificationRepository,
    IBlockRepository blockRepository,
    IUserRepository userRepository,
    IRealtimeNotifier realtimeNotifier,
    ILogger<NotificationService> logger)
    : INotificationService
{
    public async Task CreateAsync(
        Ulid recipientId,
        Ulid actorId,
        NotificationType type,
        Ulid entityId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default)
    {
        // Never notify yourself (liking/commenting on your own content).
        if (recipientId == actorId)
            return;

        try
        {
            // §8.1: no notifications are created between users with a block relationship.
            if (await blockRepository.ExistsBetweenAsync(recipientId, actorId, cancellationToken))
                return;

            // Muted types are never created (not merely hidden): unmuting reveals only
            // future events, and the unread count stays truthful without client math.
            var preferences = await userRepository.GetPreferencesAsync(recipientId, cancellationToken);
            if (preferences is not null && preferences.Mutes(type))
                return;

            // One ping per sender until read: a 30-message burst from one person is a single
            // notification; a second sender still notifies. Scoped to messages — likes and
            // comments on different entities each carry their own information.
            if (type == NotificationType.NewMessage
                && await notificationRepository.ExistsUnreadAsync(
                    recipientId, actorId, type, cancellationToken))
                return;

            var notification = Notification.Create(recipientId, actorId, type, entityId, occurredAt);
            await notificationRepository.AddAsync(notification, cancellationToken);

            // Live bell push (§11.1). Best-effort: inside the same try, so a push failure is
            // logged and swallowed just like a persistence failure.
            await realtimeNotifier.NotificationReceivedAsync(
                recipientId,
                new RealtimeNotification(type.ToString(), actorId.ToString(), entityId.ToString(), occurredAt),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: a notification failure must not surface to the triggering action.
            logger.LogError(
                ex,
                "Failed to create {Type} notification for recipient {RecipientId} from actor {ActorId}",
                type,
                recipientId,
                actorId);
        }
    }
}
