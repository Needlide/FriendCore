using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetNotificationTypes;

public sealed record GetNotificationTypesQuery : IRequest<ErrorOr<NotificationTypesResponse>>;
