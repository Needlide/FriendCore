using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.SearchMessages;

public sealed record SearchMessagesQuery(Ulid ConversationId, string? Term)
    : IRequest<ErrorOr<MessageFeedResponse>>;