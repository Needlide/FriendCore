namespace FriendCore.Application.Common.Models;

/// <summary>
///     Lightweight bell push: enough to show a toast and bump the unread badge. The client
///     refetches GET /notifications for the rendered list (actor name, avatar, etc.), so the
///     write path stays cheap (no actor lookup just to push).
/// </summary>
public sealed record RealtimeNotification(
    string Type,
    string ActorId,
    string EntityId,
    DateTimeOffset CreatedAt);
