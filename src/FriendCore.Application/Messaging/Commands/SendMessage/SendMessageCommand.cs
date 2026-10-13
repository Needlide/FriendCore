using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.SendMessage;

/// <param name="ReplyToMessageId">
///     Optional: the message this replies to. Validated conversation-scoped like every other
///     message reference here, so an id from another thread is a 404 rather than a cross-thread
///     quote.
/// </param>
public sealed record SendMessageCommand(
    Ulid ConversationId,
    string? Content,
    Ulid? MediaId = null,
    Ulid? ReplyToMessageId = null) : IRequest<ErrorOr<MessageResponse>>;
