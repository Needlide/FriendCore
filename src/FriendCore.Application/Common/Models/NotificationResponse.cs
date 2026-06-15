namespace FriendCore.Application.Common.Models;

public sealed record NotificationResponse(
    string Id,
    string Type,
    UserSummaryResponse Actor,
    string EntityId,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationListResponse(IReadOnlyList<NotificationResponse> Notifications);

public sealed record UnreadCountResponse(int Count);
