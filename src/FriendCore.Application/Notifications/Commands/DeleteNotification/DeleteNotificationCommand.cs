using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Notifications.Commands.DeleteNotification;

public sealed record DeleteNotificationCommand(Ulid NotificationId)
    : IRequest<ErrorOr<UnreadCountResponse>>;
