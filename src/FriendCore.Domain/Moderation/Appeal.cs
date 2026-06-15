using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Moderation;

public sealed class Appeal : AggregateRoot<Ulid>
{
    public const int MaxReasonLength = 2000;
    public const int MaxReviewNoteLength = 2000;

    private Appeal(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Appeal()
    {
    }

    public Ulid AuditLogId { get; private set; }
    public Ulid AppellantId { get; private set; }
    public string Reason { get; private set; } = null!;
    public AppealStatus Status { get; private set; }

    // §13.3: response within 3–7 days depending on severity. Queue sorts by this ascending.
    public DateTimeOffset Deadline { get; private set; }
    public Ulid? ReviewedByModeratorId { get; private set; }
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static ErrorOr<Appeal> Create(
        Ulid auditLogId,
        Ulid appellantId,
        string? reason,
        ModerationActionType originalAction,
        DateTimeOffset createdAt)
    {
        if (!IsAppealable(originalAction))
            return AppealErrors.NotAppealable;

        var trimmed = reason?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return AppealErrors.ReasonRequired;

        if (trimmed.Length > MaxReasonLength)
            return AppealErrors.ReasonTooLong;

        return new Appeal(Ulid.NewUlid(), createdAt)
        {
            AuditLogId = auditLogId,
            AppellantId = appellantId,
            Reason = trimmed,
            Status = AppealStatus.Pending,
            Deadline = DeadlineFor(originalAction, createdAt)
        };
    }

    public ErrorOr<Success> Review(AppealStatus outcome, Ulid reviewerId, string? note, DateTimeOffset at)
    {
        if (Status != AppealStatus.Pending)
            return AppealErrors.AlreadyReviewed;

        var trimmed = note?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return AppealErrors.ReviewNoteRequired;

        if (trimmed.Length > MaxReviewNoteLength)
            return Error.Validation("Appeal.ReviewNoteTooLong",
                $"Review note must not exceed {MaxReviewNoteLength} characters.");

        Status = outcome;
        ReviewedByModeratorId = reviewerId;
        ReviewNote = trimmed;
        ResolvedAt = at;
        return Result.Success;
    }

    // Dismissals aren't actions against a user, so there's nothing to appeal.
    public static bool IsAppealable(ModerationActionType action) =>
        action != ModerationActionType.ReportDismissed;

    public static DateTimeOffset DeadlineFor(ModerationActionType action, DateTimeOffset createdAt) =>
        createdAt.AddDays(action switch
        {
            ModerationActionType.PermanentBan => 3,
            ModerationActionType.LegalReferral => 3,
            ModerationActionType.TemporarySuspension => 5,
            ModerationActionType.TemporaryRestriction => 5,
            _ => 7
        });
}
