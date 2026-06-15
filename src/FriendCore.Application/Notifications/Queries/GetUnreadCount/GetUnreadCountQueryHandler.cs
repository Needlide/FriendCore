using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Notifications;
using MediatR;

namespace FriendCore.Application.Notifications.Queries.GetUnreadCount;

internal sealed class GetUnreadCountQueryHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository)
    : IRequestHandler<GetUnreadCountQuery, ErrorOr<UnreadCountResponse>>
{
    public async Task<ErrorOr<UnreadCountResponse>> Handle(
        GetUnreadCountQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var count = await notificationRepository.CountUnreadAsync(userId, cancellationToken);
        return new UnreadCountResponse(count);
    }
}
