using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.UpdateNotificationTypes;

/// <summary>Whole-set replace; returns the list actually stored (unknown names dropped).</summary>
public sealed record UpdateNotificationTypesCommand(IReadOnlyList<string> MutedTypes)
    : IRequest<ErrorOr<NotificationTypesResponse>>;
