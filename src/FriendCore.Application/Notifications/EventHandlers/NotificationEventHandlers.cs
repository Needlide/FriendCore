using FriendCore.Application.Notifications.Events;
using FriendCore.Application.Notifications.Services;
using FriendCore.Domain.Notifications;
using MediatR;

namespace FriendCore.Application.Notifications.EventHandlers;

// Thin mappers from each application event to a notification. All cross-cutting rules
// (self-suppression, block check, persistence, resilience) live in NotificationService.

internal sealed class UserFollowedNotificationHandler(INotificationService notifications)
    : INotificationHandler<UserFollowedEvent>
{
    public Task Handle(UserFollowedEvent notification, CancellationToken cancellationToken) =>
        notifications.CreateAsync(
            notification.FollowedId,
            notification.FollowerId,
            NotificationType.NewFollower,
            notification.FollowerId,
            notification.OccurredAt,
            cancellationToken);
}

internal sealed class PostLikedNotificationHandler(INotificationService notifications)
    : INotificationHandler<PostLikedEvent>
{
    public Task Handle(PostLikedEvent notification, CancellationToken cancellationToken) =>
        notifications.CreateAsync(
            notification.PostAuthorId,
            notification.ActorId,
            NotificationType.NewLike,
            notification.PostId,
            notification.OccurredAt,
            cancellationToken);
}

internal sealed class PostCommentedNotificationHandler(INotificationService notifications)
    : INotificationHandler<PostCommentedEvent>
{
    public Task Handle(PostCommentedEvent notification, CancellationToken cancellationToken) =>
        notifications.CreateAsync(
            notification.PostAuthorId,
            notification.ActorId,
            NotificationType.NewComment,
            notification.PostId,
            notification.OccurredAt,
            cancellationToken);
}

internal sealed class MessageSentNotificationHandler(INotificationService notifications)
    : INotificationHandler<MessageSentEvent>
{
    public Task Handle(MessageSentEvent notification, CancellationToken cancellationToken) =>
        notifications.CreateAsync(
            notification.RecipientId,
            notification.SenderId,
            NotificationType.NewMessage,
            notification.ConversationId,
            notification.OccurredAt,
            cancellationToken);
}
