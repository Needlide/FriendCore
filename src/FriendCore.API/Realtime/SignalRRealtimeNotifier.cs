using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using Microsoft.AspNetCore.SignalR;

namespace FriendCore.API.Realtime;

/// <summary>
///     IRealtimeNotifier over SignalR. IHubContext is the server-side handle to push to connected
///     clients without being inside a hub method. Clients.User(id) targets every connection that
///     IUserIdProvider mapped to that user; if the user has no live connections, SendAsync is a
///     harmless no-op. Resilient by design — never throws, so a push failure can't affect the
///     triggering action even though callers don't wrap it.
/// </summary>
public sealed class SignalRRealtimeNotifier(
    IHubContext<NotificationHub> notificationHub,
    IHubContext<MessagingHub> messagingHub,
    ILogger<SignalRRealtimeNotifier> logger)
    : IRealtimeNotifier
{
    public async Task NotificationReceivedAsync(
        Ulid recipientId,
        RealtimeNotification notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await notificationHub.Clients
                .User(recipientId.ToString())
                .SendAsync(NotificationHub.ReceiveMethod, notification, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push notification to {RecipientId}", recipientId);
        }
    }

    public async Task MessageReceivedAsync(
        Ulid recipientId,
        MessageResponse message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await messagingHub.Clients
                .User(recipientId.ToString())
                .SendAsync(MessagingHub.ReceiveMethod, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push message to {RecipientId}", recipientId);
        }
    }
}
