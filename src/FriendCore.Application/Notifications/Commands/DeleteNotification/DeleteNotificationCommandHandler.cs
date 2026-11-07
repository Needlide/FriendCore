using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Notifications;
using MediatR;

namespace FriendCore.Application.Notifications.Commands.DeleteNotification;

internal sealed class DeleteNotificationCommandHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository)
    : IRequestHandler<DeleteNotificationCommand, ErrorOr<UnreadCountResponse>>
{
    public async Task<ErrorOr<UnreadCountResponse>> Handle(
        DeleteNotificationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var notification = await notificationRepository.GetByIdAsync(
            request.NotificationId, userId, cancellationToken);
        if (notification is null)
            return NotificationErrors.NotFound;

        await notificationRepository.DeleteAsync(notification, cancellationToken);

        // Deleting an unread row removes it from the badge; the fresh count is authoritative.
        var unread = await notificationRepository.CountUnreadAsync(userId, cancellationToken);
        return new UnreadCountResponse(unread);
    }
}
