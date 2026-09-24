using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.ClearNotificationSnooze;

public sealed record ClearNotificationSnoozeCommand : IRequest<ErrorOr<NotificationSnoozeResponse>>;
