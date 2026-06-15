using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Notifications.Events;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.SendMessage;

internal sealed class SendMessageCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    IPublisher publisher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SendMessageCommand, ErrorOr<MessageResponse>>
{
    public async Task<ErrorOr<MessageResponse>> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        // §8.2: a block placed after the conversation existed must stop new messages. 404 so the
        // sender cannot tell a block from a vanished conversation.
        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        var now = dateTimeProvider.UtcNow;
        var messageResult = Message.Create(conversation.Id, userId, request.Content, now);
        if (messageResult.IsError)
            return messageResult.Errors;

        var message = messageResult.Value;
        conversation.RecordMessage(now);
        await messagingRepository.AddMessageAsync(message, cancellationToken);

        if (conversation.OtherParticipantId(userId) is { } recipientId)
            await publisher.Publish(
                new MessageSentEvent(recipientId, userId, conversation.Id, now), cancellationToken);

        return MessageResponse.Create(message);
    }
}
