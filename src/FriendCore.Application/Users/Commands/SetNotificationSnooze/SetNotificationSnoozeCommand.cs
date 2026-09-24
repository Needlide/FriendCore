using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.SetNotificationSnooze;

public sealed record SetNotificationSnoozeCommand(DateTimeOffset Until) : IRequest<ErrorOr<NotificationSnoozeResponse>>;
