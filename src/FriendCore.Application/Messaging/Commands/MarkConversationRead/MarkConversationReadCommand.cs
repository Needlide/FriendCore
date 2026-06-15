using ErrorOr;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.MarkConversationRead;

public sealed record MarkConversationReadCommand(Ulid ConversationId) : IRequest<ErrorOr<Success>>;
