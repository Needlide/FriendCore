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

    public async Task MessageEditedAsync(
        Ulid conversationId,
        IEnumerable<Ulid> participantIds,
        MessageResponse message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await PushToParticipants(participantIds, MessagingHub.EditedMethod, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push edit for conversation {ConversationId}", conversationId);
        }
    }

    public async Task MessageDeletedAsync(
        Ulid conversationId,
        IEnumerable<Ulid> participantIds,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await PushToParticipants(
                participantIds, MessagingHub.DeletedMethod, new MessageDeletedPayload(messageId), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push delete for conversation {ConversationId}", conversationId);
        }
    }

    public async Task MessageReactionChangedAsync(
        Ulid conversationId,
        IEnumerable<Ulid> participantIds,
        Ulid messageId,
        ReactionResponse? reaction,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // A null reaction travels as a zero-count payload rather than null, so the client
            // handles one shape: "count is 0" is the signal to drop the chip.
            var payload = new MessageReactionChangedPayload(
                messageId.ToString(),
                reaction?.Emoji ?? string.Empty,
                reaction?.Count ?? 0,
                reaction?.Reacted ?? false);

            await PushToParticipants(
                participantIds, MessagingHub.ReactionChangedMethod, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push reaction change for conversation {ConversationId}", conversationId);
        }
    }

    // Both sides of a direct conversation, fanned out per user rather than as a group: the
    // client joins no groups (§11.1), and a two-participant set makes a group a worse fit.
    private Task PushToParticipants(
        IEnumerable<Ulid> participantIds,
        string method,
        object payload,
        CancellationToken cancellationToken)
    {
        var sends = participantIds
            .Select(id => messagingHub.Clients
                .User(id.ToString())
                .SendAsync(method, payload, cancellationToken))
            .ToArray();

        return Task.WhenAll(sends);
    }
}

/// <summary>Minimal delete signal: the client drops the id from its thread.</summary>
public sealed record MessageDeletedPayload(string MessageId);

/// <summary>
///     One emoji's new state on one message. <see cref="Count" /> of 0 means the last holder
///     released it and the client drops the chip — the same vanish-don't-tombstone shape a
///     deleted message uses.
/// </summary>
public sealed record MessageReactionChangedPayload(
    string MessageId,
    string Emoji,
    int Count,
    bool Reacted);
