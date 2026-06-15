using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.StartConversation;

/// <summary>Find-or-create a 1:1 conversation with the user identified by <paramref name="Username" />.</summary>
public sealed record StartConversationCommand(string Username) : IRequest<ErrorOr<ConversationResponse>>;
