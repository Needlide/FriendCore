using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.EditMessage;

/// <summary>
/// Rewrites the text of an own message. <paramref name="ConversationId" /> scopes the lookup:
/// a message id from another conversation must be indistinguishable from one that does not exist.
/// </summary>
public sealed record EditMessageCommand(Ulid ConversationId, Ulid MessageId, string Content)
    : IRequest<ErrorOr<MessageResponse>>;
