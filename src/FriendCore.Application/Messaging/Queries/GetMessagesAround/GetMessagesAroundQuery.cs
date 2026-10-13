using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.GetMessagesAround;

public sealed record GetMessagesAroundQuery(Ulid ConversationId, string MessageId)
    : IRequest<ErrorOr<MessageWindowResponse>>;
