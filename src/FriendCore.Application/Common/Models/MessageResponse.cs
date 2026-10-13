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
    MediaResponse? Media,
    DateTimeOffset? EditedAt = null,
    IReadOnlyList<ReactionResponse>? Reactions = null,
    int Version = 0,
    MessageReferenceResponse? ReplyTo = null)
{
    public static MessageResponse Create(
        Message message,
        MediaResponse? media = null,
        IReadOnlyList<ReactionResponse>? reactions = null,
        MessageReferenceResponse? replyTo = null) =>
        new(
            message.Id.ToString(),
            message.ConversationId.ToString(),
            message.SenderId.ToString(),
            message.Content,
            message.Status.ToString(),
            message.CreatedAt,
            media,
            message.EditedAt,
            // Normalized to an empty list, never null: a message with no reactions is the common
            // case, and this record doubles as the realtime payload for MessageReceived and
            // MessageEdited, so it has to serialize the same predictable shape for both REST
            // and the hub. Create is the only construction path, so the payload cannot leak null.
            reactions ?? [],
            // Carried for the same reason: this record is the MessageEdited payload, so a client
            // that did not receive the new version would 409 on its next edit of its own copy.
            message.Version,
            // Null unless this message replies to another. Like the rest of this record it rides
            // the hub payloads too — a reply arriving live has to render its quote, or the bubble
            // would briefly appear to be the first word in the thread.
            replyTo);
}

public sealed record MessageFeedResponse(IReadOnlyList<MessageResponse> Messages, string? NextCursor);
/// <summary>
///     A two-way slice of a conversation around one message, for jumping to it from search.
/// </summary>
/// <remarks>
///     Separate from <see cref="MessageFeedResponse" /> rather than reusing it with a null cursor:
///     this one pages in both directions, and collapsing that into one cursor would make the
///     client infer the direction from which field was null.
/// </remarks>
public sealed record MessageWindowResponse(
    IReadOnlyList<MessageResponse> Messages,
    string? OlderCursor,
    string? NewerCursor);
