using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Messaging.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.SearchMessages;

internal sealed class SearchMessagesQueryHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    MediaResponseFactory mediaResponseFactory,
    ReplyReferenceFactory replyReferenceFactory)
    : IRequestHandler<SearchMessagesQuery, ErrorOr<MessageFeedResponse>>
{
    /// <summary>
    ///     Same page size as the feed. Search is capped rather than paged: a result list long
    ///     enough to need scrolling is a sign the term is too broad, not a sign to build
    ///     pagination over it.
    /// </summary>
    private const int Limit = 50;

    /// <summary>
    ///     Below this a single character matches most of any conversation and returns a wall of
    ///     noise the user did not ask for. Rejected rather than silently capped, so the client can
    ///     explain why nothing happened.
    /// </summary>
    private const int MinimumTermLength = 2;

    public async Task<ErrorOr<MessageFeedResponse>> Handle(
        SearchMessagesQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var term = request.Term?.Trim() ?? string.Empty;
        if (term.Length < MinimumTermLength)
            return MessagingErrors.SearchTermTooShort;

        // Same scoping as reading the thread: participant first, then the block check. Search is
        // a read of the conversation, so it inherits both — a blocked pair cannot be searched by
        // either side, exactly as they cannot read it.
        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        var hits = await messagingRepository.SearchMessagesAsync(
            conversation.Id, userId, term, Limit, cancellationToken);

        // Same three batched lookups as the feed page, for the same reason: one query each rather
        // than one per message. Search results render as ordinary bubbles, so they need media,
        // reactions and reply targets too.
        var mediaByMessage = await mediaResponseFactory.ForMessagesAsync(hits, cancellationToken);
        var reactionsByMessage = await messagingRepository.GetReactionsAsync(
            hits.Select(message => message.Id).ToList(), userId, cancellationToken);
        var repliesByMessage = await replyReferenceFactory.ForMessagesAsync(hits, cancellationToken);

        var items = hits
            .Select(message => MessageResponse.Create(
                message,
                mediaByMessage.GetValueOrDefault(message.Id),
                ReactionResponse.FromTallies(reactionsByMessage.GetValueOrDefault(message.Id)),
                repliesByMessage.GetValueOrDefault(message.Id)))
            .ToList();

        // No cursor: search is capped at Limit and has nothing to page to. Reusing MessageFeedResponse
        // rather than inventing a near-identical shape for a result list that happens to have no
        // next page — the client already knows how to render one.
        return new MessageFeedResponse(items, null);
    }
}