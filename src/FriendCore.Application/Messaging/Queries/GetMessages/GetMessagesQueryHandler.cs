using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Messaging.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.GetMessages;

internal sealed class GetMessagesQueryHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    MediaResponseFactory mediaResponseFactory)
    : IRequestHandler<GetMessagesQuery, ErrorOr<MessageFeedResponse>>
{
    private const int PageSize = 20;

    public async Task<ErrorOr<MessageFeedResponse>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        // A block makes the conversation "not exist" for both sides (§8.2), same as the write path.
        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        var cursor = MessageCursor.Decode(request.Cursor);
        var messages = await messagingRepository.GetMessagesAsync(
            conversation.Id, cursor, PageSize + 1, cancellationToken);

        var hasMore = messages.Count > PageSize;
        var page = hasMore ? messages.Take(PageSize).ToList() : messages;

        // One media lookup for the whole page, not one per message — same shape as the feed.
        var mediaByMessage = await mediaResponseFactory.ForMessagesAsync(page, cancellationToken);

        var items = page
            .Select(message => MessageResponse.Create(message, mediaByMessage.GetValueOrDefault(message.Id)))
            .ToList();
        var nextCursor = hasMore ? MessageCursor.Encode(page[^1].CreatedAt, page[^1].Id) : null;

        return new MessageFeedResponse(items, nextCursor);
    }
}
