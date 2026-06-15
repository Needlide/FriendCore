using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Common.Models;

public sealed record MessageResponse(
    string Id,
    string ConversationId,
    string SenderId,
    string Content,
    string Status,
    DateTimeOffset CreatedAt)
{
    public static MessageResponse Create(Message message) =>
        new(
            message.Id.ToString(),
            message.ConversationId.ToString(),
            message.SenderId.ToString(),
            message.Content,
            message.Status.ToString(),
            message.CreatedAt);
}

public sealed record MessageFeedResponse(IReadOnlyList<MessageResponse> Messages, string? NextCursor);
