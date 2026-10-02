using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using FriendCore.Domain.Users;

namespace FriendCore.Application.Moderation.Services;

internal sealed class ModerationService(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IPostRepository postRepository,
    IMessagingRepository messagingRepository,
    IAdminAuditLogRepository auditLogRepository,
    IDateTimeProvider dateTimeProvider)
    : IModerationService
{
    public async Task<ErrorOr<Success>> ExecuteAsync(
        ModerationActionType action,
        ReportTargetType targetType,
        Ulid targetId,
        string reason,
        int? suspensionDays,
        Report? report,
        CancellationToken cancellationToken = default)
    {
        if (currentUserService.UserId is not { } moderatorId)
            return CommonErrors.Unauthorized;

        var now = dateTimeProvider.UtcNow;

        // Resolve the subject (a post for content removal, a user for everything with teeth) and
        // apply the runtime enforcement as a tracked mutation — no save yet.
        ReportTargetType auditTargetType;
        Ulid auditTargetId;

        switch (action)
        {
            case ModerationActionType.ContentRemoval:
                // Removal acts on content. A User target has no content to remove, so the
                // ladder escalates to a warning/ban instead of silently no-oping.
                if (targetType is not (ReportTargetType.Post or ReportTargetType.Message))
                    return ModerationErrors.ContentRemovalRequiresContent;

                if (targetType == ReportTargetType.Post)
                {
                    var post = await postRepository.GetByIdForModerationAsync(targetId, cancellationToken);
                    if (post is null)
                        return ModerationErrors.TargetNotFound;

                    if (post.DeletedAt is null)
                        postRepository.StageSoftDelete(post);

                    auditTargetType = ReportTargetType.Post;
                    auditTargetId = post.Id;
                }
                else
                {
                    // Block-filter-free lookup: the message may be soft-deleted already, and a
                    // moderator still needs to see (and restore) what they actioned.
                    var message = await messagingRepository.GetMessageForModerationAsync(
                        targetId, cancellationToken);
                    if (message is null)
                        return ModerationErrors.TargetNotFound;

                    if (message.DeletedAt is null)
                        messagingRepository.StageSoftDelete(message, now);

                    auditTargetType = ReportTargetType.Message;
                    auditTargetId = message.Id;
                }

                break;

            case ModerationActionType.TemporarySuspension:
            case ModerationActionType.PermanentBan:
            case ModerationActionType.Warning:
            case ModerationActionType.TemporaryRestriction:
            case ModerationActionType.LegalReferral:
                var subject = await ResolveSubjectUserAsync(targetType, targetId, cancellationToken);
                if (subject is null)
                    return ModerationErrors.TargetNotFound;

                if (action == ModerationActionType.TemporarySuspension)
                {
                    if (suspensionDays is not > 0)
                        return ModerationErrors.InvalidSuspensionDuration;
                    subject.Suspend(now.AddDays(suspensionDays.Value), now);
                }
                else if (action == ModerationActionType.PermanentBan)
                {
                    subject.Ban(now);
                }

                // Warning / TemporaryRestriction / LegalReferral: recorded in the log only.
                auditTargetType = ReportTargetType.User;
                auditTargetId = subject.Id;
                break;

            case ModerationActionType.ReportDismissed:
                // No enforcement; the audit entry just records the dismissal against the target.
                auditTargetType = targetType;
                auditTargetId = targetId;
                break;

            default:
                return ModerationErrors.TargetNotFound;
        }

        if (report is not null)
        {
            var outcome = action == ModerationActionType.ReportDismissed
                ? report.Dismiss(moderatorId, now)
                : report.Resolve(moderatorId, now);
            if (outcome.IsError)
                return outcome.Errors;
        }

        var entry = AdminAuditLog.Create(
            moderatorId, action, auditTargetType, auditTargetId, reason,
            currentUserService.IpAddress, report?.Id, now);

        // The sole SaveChanges — commits the enforcement mutation, the report resolution, and the
        // audit entry together. No action can persist without its log.
        await auditLogRepository.AddAndSaveAsync(entry, cancellationToken);
        return Result.Success;
    }

    private async Task<User?> ResolveSubjectUserAsync(
        ReportTargetType targetType,
        Ulid targetId,
        CancellationToken cancellationToken)
    {
        if (targetType == ReportTargetType.User)
            return await userRepository.GetByIdIgnoringBlocksAsync(targetId, cancellationToken);

        // Content targets → the action lands on whoever wrote the content, so a suspension for
        // an abusive DM reaches the sender and not the conversation.
        var authorId = targetType == ReportTargetType.Post
            ? (await postRepository.GetByIdForModerationAsync(targetId, cancellationToken))?.AuthorId
            : (await messagingRepository.GetMessageForModerationAsync(targetId, cancellationToken))?.SenderId;

        return authorId is { } id
            ? await userRepository.GetByIdIgnoringBlocksAsync(id, cancellationToken)
            : null;
    }
}
