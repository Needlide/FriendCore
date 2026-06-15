using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.GetConversations;

internal sealed class GetConversationsQueryHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IUserRepository userRepository,
    IBlockRepository blockRepository)
    : IRequestHandler<GetConversationsQuery, ErrorOr<ConversationListResponse>>
{
    private const int MaxResults = 50;

    public async Task<ErrorOr<ConversationListResponse>> Handle(
        GetConversationsQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var conversations = await messagingRepository.GetConversationsForUserAsync(
            userId, MaxResults, cancellationToken);
        if (conversations.Count == 0)
            return new ConversationListResponse([]);

        // Conversations whose counterpart is in a block relationship are hidden entirely — a
        // blocked user stays invisible, consistent with §8.1. GetCounterpartIdsAsync is the
        // Phase 4 block-list lookup.
        var blocked = (await blockRepository.GetCounterpartIdsAsync(userId, cancellationToken)).ToHashSet();

        var visible = conversations
            .Select(conversation => (Conversation: conversation, OtherId: conversation.OtherParticipantId(userId)))
            .Where(entry => entry.OtherId is { } other && !blocked.Contains(other))
            .ToList();

        if (visible.Count == 0)
            return new ConversationListResponse([]);

        var others = (await userRepository.GetByIdsAsync(
                visible.Select(entry => entry.OtherId!.Value).Distinct().ToList(), cancellationToken))
            .ToDictionary(user => user.Id);

        var responses = new List<ConversationResponse>(visible.Count);
        foreach (var (conversation, otherId) in visible)
        {
            if (!others.TryGetValue(otherId!.Value, out var other))
                continue;

            var unread = await messagingRepository.CountUnreadAsync(
                conversation.Id, userId, conversation.LastReadAtFor(userId), cancellationToken);

            responses.Add(new ConversationResponse(
                conversation.Id.ToString(),
                UserSummaryResponse.FromUser(other),
                conversation.LastMessageAt,
                unread));
        }

        return new ConversationListResponse(responses);
    }
}
