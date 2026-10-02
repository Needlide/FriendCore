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

    // Push an edit to BOTH participants: the recipient's open thread shows the new text, and
    // the sender's other devices (phone, another tab) would otherwise keep showing the old
    // wording indefinitely, since MessageReceived never reaches them.
    Task MessageEditedAsync(
        Ulid conversationId,
        IEnumerable<Ulid> participantIds,
        MessageResponse message,
        CancellationToken cancellationToken = default);

    // Same audience as an edit. Carries ids only — the content is already gone, and pushing
    // a tombstone would tell the client more than a reconcile refetch would.
    Task MessageDeletedAsync(
        Ulid conversationId,
        IEnumerable<Ulid> participantIds,
        string messageId,
        CancellationToken cancellationToken = default);
}
