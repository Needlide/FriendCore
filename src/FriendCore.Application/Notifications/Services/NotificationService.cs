using FriendCore.Domain.Notifications;
using FriendCore.Domain.Users;
using Microsoft.Extensions.Logging;

namespace FriendCore.Application.Notifications.Services;

internal sealed class NotificationService(
    INotificationRepository notificationRepository,
    IBlockRepository blockRepository,
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

            var notification = Notification.Create(recipientId, actorId, type, entityId, occurredAt);
            await notificationRepository.AddAsync(notification, cancellationToken);
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
