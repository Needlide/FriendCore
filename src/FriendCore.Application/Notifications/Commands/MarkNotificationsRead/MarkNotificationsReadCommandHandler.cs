using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Notifications;
using MediatR;

namespace FriendCore.Application.Notifications.Commands.MarkNotificationsRead;

internal sealed class MarkNotificationsReadCommandHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository)
    : IRequestHandler<MarkNotificationsReadCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        MarkNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        await notificationRepository.MarkAllReadAsync(userId, cancellationToken);
        return Result.Success;
    }
}
