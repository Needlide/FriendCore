using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ChangeUserRole;

/// <summary>Admin-only role assignment (RBAC management). Reason is mandatory and audit-logged.</summary>
public sealed record ChangeUserRoleCommand(string Username, UserRoleInput Role, string Reason)
    : IRequest<ErrorOr<Success>>;
