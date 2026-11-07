using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Notifications;
using MediatR;

namespace FriendCore.Application.Notifications.Commands.MarkNotificationRead;

internal sealed class MarkNotificationReadCommandHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository)
    : IRequestHandler<MarkNotificationReadCommand, ErrorOr<UnreadCountResponse>>
{
    public async Task<ErrorOr<UnreadCountResponse>> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var notification = await notificationRepository.GetByIdAsync(
            request.NotificationId, userId, cancellationToken);
        if (notification is null)
            return NotificationErrors.NotFound;

        // Idempotent: marking an already-read notification read is a no-op, but still returns
        // the current badge count so the client does not have to guess.
        if (!notification.IsRead)
        {
            notification.MarkRead();
            await notificationRepository.MarkReadAsync(notification, cancellationToken);
        }

        var unread = await notificationRepository.CountUnreadAsync(userId, cancellationToken);
        return new UnreadCountResponse(unread);
    }
}
