using ErrorOr;
using MediatR;

namespace FriendCore.Application.Notifications.Commands.MarkNotificationsRead;

/// <summary>Marks all of the current user's unread notifications as read (clears the bell).</summary>
public sealed record MarkNotificationsReadCommand : IRequest<ErrorOr<Success>>;
