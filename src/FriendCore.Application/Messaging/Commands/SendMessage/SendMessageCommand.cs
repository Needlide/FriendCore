using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.SendMessage;

public sealed record SendMessageCommand(
    Ulid ConversationId,
    string? Content,
    Ulid? MediaId = null) : IRequest<ErrorOr<MessageResponse>>;
