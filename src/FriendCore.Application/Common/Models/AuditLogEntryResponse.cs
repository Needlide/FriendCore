using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

public sealed record AuditLogEntryResponse(
    string Id,
    UserSummaryResponse Moderator,
    string Action,
    string TargetType,
    string TargetId,
    string Reason,
    string? IpAddress,
    string? RelatedReportId,
    DateTimeOffset CreatedAt)
{
    public static AuditLogEntryResponse Create(AdminAuditLog entry, UserSummaryResponse moderator) =>
        new(
            entry.Id.ToString(),
            moderator,
            entry.Action.ToString(),
            entry.TargetType.ToString(),
            entry.TargetId.ToString(),
            entry.Reason,
            entry.IpAddress,
            entry.RelatedReportId?.ToString(),
            entry.CreatedAt);
}

public sealed record AuditLogResponse(IReadOnlyList<AuditLogEntryResponse> Entries);
