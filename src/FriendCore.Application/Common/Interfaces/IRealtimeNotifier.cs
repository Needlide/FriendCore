using FriendCore.Application.Common.Models;

namespace FriendCore.Application.Common.Interfaces;

/// <summary>
///     Real-time push (§11.1). The SignalR implementation lives in the API layer (hubs are web
///     endpoints); the application depends only on this abstraction, so command/notification
///     logic stays testable and transport-agnostic. Implementations must be best-effort — a
///     failed push can never break the action that triggered it.
/// </summary>
public interface IRealtimeNotifier
{
    // Push to the recipient's notification bell across all their connected devices.
    Task NotificationReceivedAsync(
        Ulid recipientId,
        RealtimeNotification notification,
        CancellationToken cancellationToken = default);

    // Push a newly-sent message to the recipient so an open conversation updates live.
    Task MessageReceivedAsync(
        Ulid recipientId,
        MessageResponse message,
        CancellationToken cancellationToken = default);
}
