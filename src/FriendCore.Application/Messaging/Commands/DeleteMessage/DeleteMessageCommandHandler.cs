using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.DeleteMessage;

internal sealed class DeleteMessageCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    IRealtimeNotifier realtimeNotifier,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeleteMessageCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        // §8.2: a block makes the conversation "not exist" for both sides, writes included —
        // deleting is a write, so the recipient cannot keep messaging a blocker by other means.
        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        var message = await messagingRepository.GetMessageAsync(conversation.Id, request.MessageId, cancellationToken);
        if (message is null)
            return MessagingErrors.MessageNotFound;

        // 403 rather than 404: the recipient is a participant and can already see the message,
        // so there is nothing to protect by hiding it — and a clear "not yours" beats a 404
        // they cannot act on. (This is the PostErrors.NotAuthor precedent.)
        if (message.SenderId != userId)
            return MessagingErrors.NotMessageSender;

        // Source is Sender: only the sender can delete their own message today, and withdrawing
        // your own message is not anyone declining to receive anything — so this records no
        // consent refusal. Recipient-delete, when it lands, passes Recipient instead.
        await messagingRepository.SoftDeleteAsync(
            message, dateTimeProvider.UtcNow, MessageDeletionSource.Sender, cancellationToken);

        // Both sides, unlike MessageReceived: the recipient's open thread must drop the bubble
        // too. Best-effort by contract, so this never fails the delete.
        await realtimeNotifier.MessageDeletedAsync(
            conversation.Id,
            conversation.Participants.Select(participant => participant.UserId),
            message.Id.ToString(),
            cancellationToken);

        return Result.Success;
    }
}