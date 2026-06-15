using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.SubmitAppeal;

internal sealed class SubmitAppealCommandHandler(
    ICurrentUserService currentUserService,
    IAdminAuditLogRepository auditLogRepository,
    IPostRepository postRepository,
    IAppealRepository appealRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SubmitAppealCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(SubmitAppealCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var action = await auditLogRepository.GetByIdAsync(request.AuditLogId, cancellationToken);
        if (action is null)
            return AppealErrors.ActionNotFound;

        // Only the user the action was taken against may appeal it. For content removal the
        // affected user is the post's author. 404 (not 403) if it wasn't against you.
        var affectedUserId = await ResolveAffectedUserAsync(action, cancellationToken);
        if (affectedUserId != userId)
            return AppealErrors.NotTheAffectedUser;

        if (await appealRepository.ExistsForActionAsync(action.Id, cancellationToken))
            return AppealErrors.AlreadyAppealed;

        var appeal = Appeal.Create(action.Id, userId, request.Reason, action.Action, dateTimeProvider.UtcNow);
        if (appeal.IsError)
            return appeal.Errors;

        await appealRepository.AddAsync(appeal.Value, cancellationToken);
        return Result.Success;
    }

    private async Task<Ulid?> ResolveAffectedUserAsync(AdminAuditLog action, CancellationToken cancellationToken)
    {
        if (action.TargetType == ReportTargetType.User)
            return action.TargetId;

        var post = await postRepository.GetByIdForModerationAsync(action.TargetId, cancellationToken);
        return post?.AuthorId;
    }
}
