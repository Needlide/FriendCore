using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.EditMessage;

/// <summary>
/// Rewrites the text of an own message. <paramref name="ConversationId" /> scopes the lookup:
/// a message id from another conversation must be indistinguishable from one that does not exist.
/// </summary>
/// <param name="ExpectedVersion">
///     The <c>Message.Version</c> the caller was looking at. Required: a rewrite presented
///     against a stale view would silently discard whatever the other device saved, which is
///     the exact failure the version exists to prevent. Null is honoured by the domain but is
///     not something a real client should send.
/// </param>
public sealed record EditMessageCommand(
    Ulid ConversationId,
    Ulid MessageId,
    string Content,
    int? ExpectedVersion)
    : IRequest<ErrorOr<MessageResponse>>;
