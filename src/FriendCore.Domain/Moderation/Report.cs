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

    public static ErrorOr<Report> Create(
        Ulid reporterId,
        ReportTargetType targetType,
        Ulid targetId,
        ReportCategory category,
        string? note,
        DateTimeOffset createdAt)
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
            Status = ReportStatus.Pending
        };
    }

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
