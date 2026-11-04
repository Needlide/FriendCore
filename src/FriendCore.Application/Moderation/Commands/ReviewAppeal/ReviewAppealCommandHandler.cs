using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReviewAppeal;

internal sealed class ReviewAppealCommandHandler(
    ICurrentUserService currentUserService,
    IAppealRepository appealRepository,
    IAdminAuditLogRepository auditLogRepository,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReviewAppealCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ReviewAppealCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } moderatorId)
            return CommonErrors.Unauthorized;

        var appeal = await appealRepository.GetByIdAsync(request.AppealId, cancellationToken);
        if (appeal is null)
            return AppealErrors.NotFound;

        var action = await auditLogRepository.GetByIdAsync(appeal.AuditLogId, cancellationToken);
        if (action is null)
            return AppealErrors.ActionNotFound;

        // §13.3: a different moderator than the one who took the action must review it.
        if (action.ModeratorId == moderatorId)
            return AppealErrors.SameModerator;

        var now = dateTimeProvider.UtcNow;
        var outcome = request.Outcome.ToDomain();

        var review = appeal.Review(outcome, moderatorId, request.Note, now);
        if (review.IsError)
            return review.Errors;

        // Overturned reverses the original enforcement (§13.3 "content restored"). Staged on the
        // tracked entity; the appeal SaveChanges commits both.
        if (outcome == AppealStatus.Overturned)
            await ReverseEnforcementAsync(action, now, cancellationToken);

        await appealRepository.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }

    private async Task ReverseEnforcementAsync(
        AdminAuditLog action,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        switch (action.Action)
        {
            case ModerationActionType.ContentRemoval:
                var post = await postRepository.GetByIdForModerationAsync(action.TargetId, cancellationToken);
                if (post is { DeletedAt: not null })
                    postRepository.StageRestore(post);
                // The removal cascade is NOT reversed: likes were hard-deleted and are gone,
                // and restoring only the comments would bring back a half-thread. The post
                // returns; its reactions do not. Accepted MVP trade-off.
                break;

            case ModerationActionType.PermanentBan:
            case ModerationActionType.TemporarySuspension:
                var user = await userRepository.GetByIdIgnoringBlocksAsync(action.TargetId, cancellationToken);
                user?.Reinstate(now);
                break;

            // Warning / TemporaryRestriction / LegalReferral: nothing to reverse at runtime.
        }
    }
}
