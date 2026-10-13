using ErrorOr;
using FriendCore.Application.Common;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Messaging.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Queries.GetMessagesAround;

internal sealed class GetMessagesAroundQueryHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    MediaResponseFactory mediaResponseFactory,
    ReplyReferenceFactory replyReferenceFactory)
    : IRequestHandler<GetMessagesAroundQuery, ErrorOr<MessageWindowResponse>>
{
    /// <summary>
    ///     Messages either side of the target. Small on purpose: this is a place to stand and read
    ///     around one message, not a bulk export, and a wide window would make the jump feel slow
    ///     on a long conversation.
    /// </summary>
    private const int Radius = 25;

    public async Task<ErrorOr<MessageWindowResponse>> Handle(
        GetMessagesAroundQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // A malformed id is a 404 here, exactly as an unknown message id would be. Answering 400
        // would distinguish "not a message id" from "not your message", which is the thing the
        // conversation scoping is there to prevent.
        if (!Ulid.TryParse(request.MessageId, out var messageId))
            return MessagingErrors.MessageNotFound;

        // Identical scoping to reading the thread — participant first, then the block check. A
        // search result is only ever a message in a conversation the caller is in.
        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        var window = await messagingRepository.GetMessagesAroundAsync(
            conversation.Id, userId, messageId, Radius, cancellationToken);

        if (window.Messages.Count == 0)
            return MessagingErrors.MessageNotFound;

        // Same three batched lookups as the feed: one query each rather than one per message.
        var mediaByMessage = await mediaResponseFactory.ForMessagesAsync(window.Messages, cancellationToken);
        var reactionsByMessage = await messagingRepository.GetReactionsAsync(
            window.Messages.Select(message => message.Id).ToList(), userId, cancellationToken);
        var repliesByMessage = await replyReferenceFactory.ForMessagesAsync(window.Messages, cancellationToken);

        var items = window.Messages
            .Select(message => MessageResponse.Create(
                message,
                mediaByMessage.GetValueOrDefault(message.Id),
                ReactionResponse.FromTallies(reactionsByMessage.GetValueOrDefault(message.Id)),
                repliesByMessage.GetValueOrDefault(message.Id)))
            .ToList();

        // Cursors point OUTWARD from the window, so paging in either direction continues from its
        // edge rather than re-reading what is already on screen. Encoded here so the cursor format
        // stays in one place rather than leaking into the repository.
        // Encoded from the DOMAIN messages, not the responses: MessageResponse.Id is a string
        // for the wire, while the cursor is a typed (timestamp, Ulid) pair.
        var first = window.Messages[0];
        var last = window.Messages[^1];

        var olderCursor = window.HasOlder ? MessageCursor.Encode(first.CreatedAt, first.Id) : null;
        var newerCursor = window.HasNewer ? MessageCursor.Encode(last.CreatedAt, last.Id) : null;

        return new MessageWindowResponse(items, olderCursor, newerCursor);
    }
}
