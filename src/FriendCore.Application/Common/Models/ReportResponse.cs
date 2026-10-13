using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

/// <summary>
///     What the report's subject said when it was reported.
/// </summary>
/// <remarks>
///     Moderator-only. Null means the subject was already gone when the report was filed, which the
///     admin UI renders as "content unavailable" — a blank row would read like a capture bug.
/// </remarks>
public sealed record ReportSubjectResponse(
    string Kind,
    string Content,
    string? AuthorUsername,
    DateTimeOffset CapturedAt)
{
    public static ReportSubjectResponse? FromSnapshot(Report report) =>
        report.SubjectContent is null
            ? null
            : new ReportSubjectResponse(
                report.SubjectKind?.ToString() ?? ReportSubjectKind.Text.ToString(),
                report.SubjectContent,
                report.SubjectAuthorUsername,
                report.SubjectCapturedAt ?? report.CreatedAt);
}

public sealed record ReportResponse(
    string Id,
    string TargetType,
    string TargetId,
    string Category,
    string Priority,
    string Status,
    string? Note,
    UserSummaryResponse Reporter,
    ReportSubjectResponse? Subject,
    DateTimeOffset CreatedAt)
{
    public static ReportResponse Create(Report report, User reporter) =>
        new(
            report.Id.ToString(),
            report.TargetType.ToString(),
            report.TargetId.ToString(),
            report.Category.ToString(),
            report.Priority.ToString(),
            report.Status.ToString(),
            report.Note,
            UserSummaryResponse.FromUser(reporter),
            ReportSubjectResponse.FromSnapshot(report),
            report.CreatedAt);
}

public sealed record ReportQueueResponse(IReadOnlyList<ReportResponse> Reports);
