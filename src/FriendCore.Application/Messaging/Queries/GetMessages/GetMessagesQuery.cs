using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.GetMessages;

public sealed record GetMessagesQuery(Ulid ConversationId, string? Cursor)
    : IRequest<ErrorOr<MessageFeedResponse>>;
