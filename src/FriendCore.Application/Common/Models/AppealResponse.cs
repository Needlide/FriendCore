using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Common.Models;

public sealed record AppealResponse(
    string Id,
    string AuditLogId,
    UserSummaryResponse Appellant,
    string Reason,
    string Status,
    DateTimeOffset Deadline,
    DateTimeOffset CreatedAt)
{
    public static AppealResponse Create(Appeal appeal, UserSummaryResponse appellant) =>
        new(
            appeal.Id.ToString(),
            appeal.AuditLogId.ToString(),
            appellant,
            appeal.Reason,
            appeal.Status.ToString(),
            appeal.Deadline,
            appeal.CreatedAt);
}

public sealed record AppealQueueResponse(IReadOnlyList<AppealResponse> Appeals);

// User-facing view of an action taken against you — no moderator identity or IP leaked.
public sealed record MyModerationActionResponse(
    string Id,
    string Action,
    string Reason,
    DateTimeOffset CreatedAt);

public sealed record MyModerationActionsResponse(IReadOnlyList<MyModerationActionResponse> Actions);
