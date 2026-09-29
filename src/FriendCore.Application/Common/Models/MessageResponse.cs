using FriendCore.Domain.Messaging;

namespace FriendCore.Application.Common.Models;

/// <summary>
///     One message in a thread. <see cref="Content" /> is null for an attachment-only
///     message — the client renders the bubble from <see cref="Media" /> and nothing
///     else, rather than showing a stored placeholder nobody typed.
/// </summary>
public sealed record MessageResponse(
    string Id,
    string ConversationId,
    string SenderId,
    string? Content,
    string Status,
    DateTimeOffset CreatedAt,
    MediaResponse? Media)
{
    public static MessageResponse Create(Message message, MediaResponse? media = null) =>
        new(
            message.Id.ToString(),
            message.ConversationId.ToString(),
            message.SenderId.ToString(),
            message.Content,
            message.Status.ToString(),
            message.CreatedAt,
            media);
}

public sealed record MessageFeedResponse(IReadOnlyList<MessageResponse> Messages, string? NextCursor);
