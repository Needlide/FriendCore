using FriendCore.Domain.Users;

namespace FriendCore.Application.Common.Models;

/// <summary>§13.5 user account view: identity, current enforcement state, and moderation history.</summary>
public sealed record UserAccountResponse(
    string UserId,
    string Username,
    string DisplayName,
    string Role,
    bool IsVerified,
    bool IsBanned,
    DateTimeOffset? SuspendedUntil,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AuditLogEntryResponse> ModerationHistory)
{
    public static UserAccountResponse Create(User user, IReadOnlyList<AuditLogEntryResponse> history) =>
        new(
            user.Id.ToString(),
            user.Username,
            user.DisplayName,
            user.Role.ToString(),
            user.IsVerified,
            user.IsBanned,
            user.SuspendedUntil,
            user.CreatedAt,
            history);
}
