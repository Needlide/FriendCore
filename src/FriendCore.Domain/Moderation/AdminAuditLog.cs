using FriendCore.Domain.Common;

namespace FriendCore.Domain.Moderation;

/// <summary>
///     §10.3/§13.2: an immutable record of every moderator action — who, what, against whom, why
///     (mandatory reason), from where, when. Written in the same transaction as the action it
///     describes, so an action can never land without its log entry.
/// </summary>
public sealed class AdminAuditLog : Entity<Ulid>
{
    public const int MaxReasonLength = 1000;

    private AdminAuditLog(Ulid id, DateTimeOffset createdAt)
        : base(id, createdAt)
    {
    }

    private AdminAuditLog()
    {
    }

    public Ulid ModeratorId { get; private set; }
    public ModerationActionType Action { get; private set; }
    public ReportTargetType TargetType { get; private set; }
    public Ulid TargetId { get; private set; }
    public string Reason { get; private set; } = null!;
    public string? IpAddress { get; private set; }
    public Ulid? RelatedReportId { get; private set; }

    public static AdminAuditLog Create(
        Ulid moderatorId,
        ModerationActionType action,
        ReportTargetType targetType,
        Ulid targetId,
        string reason,
        string? ipAddress,
        Ulid? relatedReportId,
        DateTimeOffset createdAt)
    {
        return new AdminAuditLog(Ulid.NewUlid(), createdAt)
        {
            ModeratorId = moderatorId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Reason = reason.Trim(),
            IpAddress = ipAddress,
            RelatedReportId = relatedReportId
        };
    }
}
