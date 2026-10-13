using ErrorOr;
using FriendCore.Domain.Common;

namespace FriendCore.Domain.Moderation;

public sealed class Report : AggregateRoot<Ulid>
{
    public const int MaxNoteLength = 1000;

    private Report(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private Report()
    {
    }

    public Ulid ReporterId { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Ulid TargetId { get; private set; }
    public ReportCategory Category { get; private set; }
    public ReportPriority Priority { get; private set; }
    public string? Note { get; private set; }
    public ReportStatus Status { get; private set; }
    public Ulid? ResolvedByModeratorId { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    // --- Subject snapshot (§13.5) ---------------------------------------------------------
    // What the reported thing SAID at the moment it was reported, so the queue stays reviewable
    // after the subject is deleted. Every field is nullable together: a null snapshot means "the
    // subject was already gone", which the queue says explicitly rather than rendering as blank.

    public ReportSubjectKind? SubjectKind { get; private set; }
    public string? SubjectContent { get; private set; }
    public Ulid? SubjectAuthorId { get; private set; }

    /// <summary>The author's name as it was then — usernames are editable, so this is a copy.</summary>
    public string? SubjectAuthorUsername { get; private set; }

    public DateTimeOffset? SubjectCapturedAt { get; private set; }

    public static ErrorOr<Report> Create(
        Ulid reporterId,
        ReportTargetType targetType,
        Ulid targetId,
        ReportCategory category,
        string? note,
        DateTimeOffset createdAt,
        ReportSubjectSnapshot? subject = null)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > MaxNoteLength)
            return ReportErrors.NoteTooLong;

        return new Report(Ulid.NewUlid(), createdAt)
        {
            ReporterId = reporterId,
            TargetType = targetType,
            TargetId = targetId,
            Category = category,
            Priority = PriorityFor(category),
            Note = trimmed,
            Status = ReportStatus.Pending,
            SubjectKind = subject?.Kind,
            SubjectContent = subject?.Content,
            SubjectAuthorId = subject?.AuthorId,
            SubjectAuthorUsername = subject?.AuthorUsername,
            SubjectCapturedAt = subject?.CapturedAt
        };
    }

    public ErrorOr<Success> Resolve(Ulid moderatorId, DateTimeOffset at)
    {
        if (Status != ReportStatus.Pending)
            return ReportErrors.AlreadyResolved;

        Status = ReportStatus.Resolved;
        ResolvedByModeratorId = moderatorId;
        ResolvedAt = at;
        PurgeSubjectSnapshotUnlessRetained();
        return Result.Success;
    }

    public ErrorOr<Success> Dismiss(Ulid moderatorId, DateTimeOffset at)
    {
        if (Status != ReportStatus.Pending)
            return ReportErrors.AlreadyResolved;

        Status = ReportStatus.Dismissed;
        ResolvedByModeratorId = moderatorId;
        ResolvedAt = at;
        PurgeSubjectSnapshotUnlessRetained();
        return Result.Success;
    }

    /// <summary>
    ///     Drops the snapshot once the report is closed, unless the category is one where the
    ///     snapshot IS the evidence and has to outlive the report.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         For everything else, keeping a copy of reported content after a moderator has looked
    ///         at it means holding content nobody needs any more, about someone who did nothing
    ///         wrong enough to justify indefinite retention. So it goes.
    ///     </para>
    ///     <para>
    ///         Illegal categories are the exception: the snapshot is the only remaining record of
    ///         what was reported, and destroying it on resolution would destroy the record of an
    ///         offence. That retention obligation is a legal question this code cannot settle — what
    ///         it can do is make the carve-out explicit and obvious rather than accidental.
    ///     </para>
    /// </remarks>
    private void PurgeSubjectSnapshotUnlessRetained()
    {
        if (RequiresEvidenceRetention(Category))
            return;

        SubjectKind = null;
        SubjectContent = null;
        SubjectAuthorId = null;
        SubjectAuthorUsername = null;
        SubjectCapturedAt = null;
    }

    /// <summary>
    ///     Categories whose snapshot survives resolution as evidence. Needs counsel before it is
    ///     relied on — see the spec note.
    /// </summary>
    public static bool RequiresEvidenceRetention(ReportCategory category) =>
        category is ReportCategory.InvolvesMinor
            or ReportCategory.NudityOrSexual
            or ReportCategory.ViolenceOrDangerous;

    /// <summary>True when this report still holds a captured copy of its subject.</summary>
    public bool HasSubjectSnapshot => SubjectContent is not null;

    // §13.5 priority is derived from the category, not chosen by the reporter.
    public static ReportPriority PriorityFor(ReportCategory category) =>
        category switch
        {
            ReportCategory.InvolvesMinor => ReportPriority.High,
            ReportCategory.ViolenceOrDangerous => ReportPriority.High,
            ReportCategory.PrivateInformation => ReportPriority.High,
            ReportCategory.Harassment => ReportPriority.Medium,
            ReportCategory.NudityOrSexual => ReportPriority.Medium,
            ReportCategory.Copyright => ReportPriority.Medium,
            ReportCategory.FlashingLights => ReportPriority.Medium,
            ReportCategory.Spam => ReportPriority.Low,
            ReportCategory.Other => ReportPriority.Low,
            _ => ReportPriority.Low
        };
}
