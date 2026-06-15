using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record ReportResponse(
    string Id,
    string TargetType,
    string TargetId,
    string Category,
    string Priority,
    string Status,
    string? Note,
    UserSummaryResponse Reporter,
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
            report.CreatedAt);
}

public sealed record ReportQueueResponse(IReadOnlyList<ReportResponse> Reports);
