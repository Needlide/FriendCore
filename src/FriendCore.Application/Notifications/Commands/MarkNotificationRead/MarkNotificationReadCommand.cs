using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Notifications.Commands.MarkNotificationRead;

public sealed record MarkNotificationReadCommand(Ulid NotificationId)
    : IRequest<ErrorOr<UnreadCountResponse>>;
