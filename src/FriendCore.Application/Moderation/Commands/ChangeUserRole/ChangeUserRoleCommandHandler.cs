using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ChangeUserRole;

internal sealed class ChangeUserRoleCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IAdminAuditLogRepository auditLogRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ChangeUserRoleCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } adminId)
            return CommonErrors.Unauthorized;

        // Block-ignoring + tracked: role management must reach any user and mutate them.
        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugIgnoringBlocksAsync(slug, cancellationToken);
        if (target is null)
            return UserErrors.NotFound;

        // Guard against self-demotion / last-admin lockout: an admin can't change their own role.
        if (target.Id == adminId)
            return ModerationErrors.CannotChangeOwnRole;

        var newRole = request.Role.ToDomain();
        if (target.Role == newRole)
            return Result.Success; // idempotent no-op

        target.AssignRole(newRole);

        // Audit the RBAC change with its mandatory reason, committed in the same save (§10.3).
        var entry = AdminAuditLog.Create(
            adminId, ModerationActionType.RoleChanged, ReportTargetType.User, target.Id,
            request.Reason, currentUserService.IpAddress, relatedReportId: null, dateTimeProvider.UtcNow);
        await auditLogRepository.AddAndSaveAsync(entry, cancellationToken);

        return Result.Success;
    }
}
