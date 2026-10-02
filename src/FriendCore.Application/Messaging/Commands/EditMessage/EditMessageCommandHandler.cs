using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.EditMessage;

internal sealed class EditMessageCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    MediaResponseFactory mediaResponseFactory,
    IRealtimeNotifier realtimeNotifier,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<EditMessageCommand, ErrorOr<MessageResponse>>
{
    public async Task<ErrorOr<MessageResponse>> Handle(EditMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        var message = await messagingRepository.GetMessageAsync(conversation.Id, request.MessageId, cancellationToken);
        if (message is null)
            return MessagingErrors.MessageNotFound;

        if (message.SenderId != userId)
            return MessagingErrors.NotMessageSender;

        // Message.Edit owns the rules (length, empty text, attachment-only) and reports why it
        // refused. Every accepted edit persists — the aggregate keeps the FIRST EditedAt, so
        // the "edited" marker still points at when the text first changed.
        var edit = message.Edit(request.Content, dateTimeProvider.UtcNow);
        if (edit.IsError)
            return edit.Errors;

        await messagingRepository.SaveChangesAsync(cancellationToken);

        var mediaByMessage = await mediaResponseFactory.ForMessagesAsync([message], cancellationToken);
        var response = MessageResponse.Create(message, mediaByMessage.GetValueOrDefault(message.Id));

        // Same audience as a delete: the recipient needs the new wording, and the sender's other
        // devices would otherwise keep rendering the old text forever.
        await realtimeNotifier.MessageEditedAsync(
            conversation.Id,
            conversation.Participants.Select(participant => participant.UserId),
            response,
            cancellationToken);

        return response;
    }
}