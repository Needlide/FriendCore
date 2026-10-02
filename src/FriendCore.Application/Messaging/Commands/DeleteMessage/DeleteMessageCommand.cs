using ErrorOr;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.DeleteMessage;

public sealed record DeleteMessageCommand(Ulid ConversationId, Ulid MessageId)
    : IRequest<ErrorOr<Success>>;