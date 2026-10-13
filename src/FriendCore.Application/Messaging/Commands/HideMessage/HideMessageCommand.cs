using ErrorOr;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.HideMessage;

public sealed record HideMessageCommand(Ulid ConversationId, Ulid MessageId)
    : IRequest<ErrorOr<Success>>;
