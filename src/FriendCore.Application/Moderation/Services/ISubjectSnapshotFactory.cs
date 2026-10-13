using FriendCore.Domain.Moderation;

namespace FriendCore.Application.Moderation.Services;

/// <summary>
///     Captures a copy of a reported subject at the moment the report is filed.
/// </summary>
/// <remarks>
///     Separate from the report handlers so all four agree on what gets captured, and so adding a
///     target type is one case here rather than four edits spread across four handlers.
/// </remarks>
public interface ISubjectSnapshotFactory
{
    /// <summary>
    ///     The snapshot for a target, or null when the subject no longer exists — which is a real
    ///     answer, not a failure: someone can report a post and have its author delete it first.
    /// </summary>
    Task<ReportSubjectSnapshot?> CaptureAsync(
        ReportTargetType targetType,
        Ulid targetId,
        CancellationToken cancellationToken = default);
}