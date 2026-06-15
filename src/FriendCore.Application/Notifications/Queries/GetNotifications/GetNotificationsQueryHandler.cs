using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Notifications;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Notifications.Queries.GetNotifications;

internal sealed class GetNotificationsQueryHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetNotificationsQuery, ErrorOr<NotificationListResponse>>
{
    private const int MaxResults = 50;

    public async Task<ErrorOr<NotificationListResponse>> Handle(
        GetNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var notifications = await notificationRepository.GetForUserAsync(userId, MaxResults, cancellationToken);
        if (notifications.Count == 0)
            return new NotificationListResponse([]);

        // Batch-load actors. A since-blocked actor is filtered out by the User block filter, so
        // their notification is dropped from the list — consistent with block invisibility.
        var actors = (await userRepository.GetByIdsAsync(
                notifications.Select(notification => notification.ActorId).Distinct().ToList(), cancellationToken))
            .ToDictionary(user => user.Id);

        var items = notifications
            .Where(notification => actors.ContainsKey(notification.ActorId))
            .Select(notification => new NotificationResponse(
                notification.Id.ToString(),
                notification.Type.ToString(),
                UserSummaryResponse.FromUser(actors[notification.ActorId]),
                notification.EntityId.ToString(),
                notification.IsRead,
                notification.CreatedAt))
            .ToList();

        return new NotificationListResponse(items);
    }
}
