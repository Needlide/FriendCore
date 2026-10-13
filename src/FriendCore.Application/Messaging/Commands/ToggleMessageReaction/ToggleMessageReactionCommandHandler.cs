using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.ToggleMessageReaction;

internal sealed class ToggleMessageReactionCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    IRealtimeNotifier realtimeNotifier,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ToggleMessageReactionCommand, ErrorOr<ReactionResponse>>
{
    public async Task<ErrorOr<ReactionResponse>> Handle(
        ToggleMessageReactionCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        // Same 404-across-a-block as every other messaging action (§8.2): a blocked thread must
        // not be reactable, and must not be distinguishable from one that never existed.
        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        // Conversation-scoped, so a message id from another thread is a 404. The global
        // soft-delete filter makes a deleted message come back null too — you cannot react
        // to something that no longer exists.
        var message = await messagingRepository.GetMessageAsync(conversation.Id, request.MessageId, cancellationToken);
        if (message is null)
            return MessagingErrors.MessageNotFound;

        // Anyone in the thread may react to anything in it, their own messages included —
        // unlike edit and delete, which are the sender's alone. No ownership check here on
        // purpose, and no NotMessageSender: that would be the wrong error for the wrong rule.
        var held = await messagingRepository.ReactionExistsAsync(
            message.Id, userId, request.Emoji, cancellationToken);

        if (held)
        {
            await messagingRepository.RemoveReactionAsync(message.Id, userId, request.Emoji, cancellationToken);
        }
        else
        {
            var reaction = MessageReaction.Create(
                message.Id, userId, request.Emoji, dateTimeProvider.UtcNow);
            if (reaction.IsError)
                return reaction.Errors;

            await messagingRepository.AddReactionAsync(reaction.Value, cancellationToken);
        }

        // Re-read rather than incrementing a local number: the count has to reflect the rows,
        // including a concurrent tap from the other participant that landed while we were
        // writing. One indexed query, and it cannot drift.
        var tally = await messagingRepository.GetReactionTallyAsync(
            message.Id, request.Emoji, userId, cancellationToken);

        // Reactions raise no notification — a ping per tap would bury the NewMessage bell in
        // noise, and unlike a like, a reaction acknowledges rather than messages.
        await realtimeNotifier.MessageReactionChangedAsync(
            conversation.Id,
            conversation.Participants.Select(participant => participant.UserId),
            message.Id,
            ReactionResponse.FromTally(tally),
            cancellationToken);

        // A null tally means this was the last reaction to that emoji, leaving the client with
        // nothing to render — the same "vanish, no tombstone" shape a deleted message has. The
        // count of 0 is the wire signal to drop the chip.
        return tally is null
            ? new ReactionResponse(request.Emoji, 0, false)
            : new ReactionResponse(tally.Emoji, tally.Count, tally.ViewerReacted);
    }
}