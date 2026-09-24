using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetNotificationSnooze;

public sealed record GetNotificationSnoozeQuery : IRequest<ErrorOr<NotificationSnoozeResponse>>;
