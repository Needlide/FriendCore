using ErrorOr;
using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Services;

/// <summary>
///     The single place a moderation action is applied. Resolves the subject, applies the runtime
///     enforcement (content removal / suspension / ban), optionally resolves the originating
///     report, and writes the mandatory AdminAuditLog entry — all in one transaction. The
///     enforcement mutations are tracked-only; the audit write is the sole SaveChanges, so an
///     action can never persist without its log entry (§10.3).
/// </summary>
internal interface IModerationService
{
    Task<ErrorOr<Success>> ExecuteAsync(
        ModerationActionType action,
        ReportTargetType targetType,
        Ulid targetId,
        string reason,
        int? suspensionDays,
        Report? report,
        CancellationToken cancellationToken = default);
}
