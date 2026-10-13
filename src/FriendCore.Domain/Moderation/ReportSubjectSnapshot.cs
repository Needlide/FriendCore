namespace FriendCore.Domain.Moderation;

/// <summary>
///     A copy of what a reported thing said, taken when the report was filed.
/// </summary>
/// <remarks>
///     <para>
///         The queue points at a <c>TargetId</c>, and ids outlive the rows behind them. A post
///         deleted by its author, a DM deleted by either party, or a user who changes their name
///         all leave a report that can no longer be acted on because nobody can see what it was
///         about. This is the copy that keeps those reports reviewable.
///     </para>
///     <para>
///         Captured at report time, not at review time — by then the subject may be gone, which is
///         the entire problem.
///     </para>
/// </remarks>
public sealed record ReportSubjectSnapshot(
    ReportSubjectKind Kind,
    string Content,
    Ulid AuthorId,
    string? AuthorUsername,
    DateTimeOffset CapturedAt);
