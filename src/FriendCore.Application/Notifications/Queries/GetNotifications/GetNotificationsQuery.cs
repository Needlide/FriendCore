using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery : IRequest<ErrorOr<NotificationListResponse>>;
