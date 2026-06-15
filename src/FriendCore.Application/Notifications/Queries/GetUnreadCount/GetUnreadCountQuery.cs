using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Notifications.Queries.GetUnreadCount;

public sealed record GetUnreadCountQuery : IRequest<ErrorOr<UnreadCountResponse>>;
