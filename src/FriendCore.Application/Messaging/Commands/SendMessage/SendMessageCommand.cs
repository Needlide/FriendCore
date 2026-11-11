using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.SendMessage;

/// <param name="MediaIds">
///     Up to four media items the caller uploaded through POST /media and owns. Validated as one
///     set: if any of them is missing, foreign, unprocessed or already attached, the whole send is
///     refused rather than silently dropping that image. Order is preserved as display order.
/// </param>
/// <param name="ReplyToMessageId">
///     Optional: the message this replies to. Validated conversation-scoped like every other
///     message reference here, so an id from another thread is a 404 rather than a cross-thread
///     quote.
/// </param>
public sealed record SendMessageCommand(
    Ulid ConversationId,
    string? Content,
    IReadOnlyList<Ulid>? MediaIds = null,
    Ulid? ReplyToMessageId = null) : IRequest<ErrorOr<MessageResponse>>;
