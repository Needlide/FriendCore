using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.GetConversations;

public sealed record GetConversationsQuery : IRequest<ErrorOr<ConversationListResponse>>;
