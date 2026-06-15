using FriendCore.Domain.Notifications;

namespace FriendCore.Application.Notifications.Services;

/// <summary>
///     The single place notifications are created. Centralises the cross-cutting rules —
///     self-suppression and the §8.1 "no notifications between blocked users" guarantee — so no
///     event handler has to remember them. Best-effort: never throws, so a notification failure
///     can never break the action that triggered it.
/// </summary>
public interface INotificationService
{
    Task CreateAsync(
        Ulid recipientId,
        Ulid actorId,
        NotificationType type,
        Ulid entityId,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken = default);
}
