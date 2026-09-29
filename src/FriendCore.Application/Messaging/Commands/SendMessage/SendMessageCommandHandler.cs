using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Notifications.Events;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.SendMessage;

internal sealed class SendMessageCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    IMediaRepository mediaRepository,
    MediaResponseFactory mediaResponseFactory,
    IRealtimeNotifier realtimeNotifier,
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

        // Same gate the post path applies: the item must exist, belong to the sender, be
        // finished processing, and not already be spoken for. Reusing MediaItem wholesale
        // means DMs inherit the upload pipeline, variants, and safety flags unchanged.
        var mediaError = await ValidateMediaAsync(request.MediaId, userId, cancellationToken);
        if (mediaError is { } mediaFailure)
            return mediaFailure;

        var messageResult = Message.Create(conversation.Id, userId, request.Content, now, request.MediaId);
        if (messageResult.IsError)
            return messageResult.Errors;

        var message = messageResult.Value;
        conversation.RecordMessage(now);
        await messagingRepository.AddMessageAsync(message, cancellationToken);

        var mediaByMessage = await mediaResponseFactory.ForMessagesAsync([message], cancellationToken);
        var response = MessageResponse.Create(message, mediaByMessage.GetValueOrDefault(message.Id));

        if (conversation.OtherParticipantId(userId) is { } recipientId)
        {
            // Bell notification (NewMessage) via the event; live message delivery via the
            // notifier so an open conversation appends it without a refetch (§11.1).
            await publisher.Publish(
                new MessageSentEvent(recipientId, userId, conversation.Id, now), cancellationToken);
            await realtimeNotifier.MessageReceivedAsync(recipientId, response, cancellationToken);
        }

        return response;
    }

    // Mirrors CreatePost's media gate, so an item that failed there cannot slip through here.
    // Returns null when there is no attachment to check.
    private async Task<Error?> ValidateMediaAsync(
        Ulid? mediaId,
        Ulid userId,
        CancellationToken cancellationToken)
    {
        if (mediaId is not { } id)
            return null;

        var items = await mediaRepository.GetByIdsAsync([id], cancellationToken);
        var media = items.FirstOrDefault();

        if (media is null)
            return MediaErrors.NotFound;

        if (media.UploaderId != userId)
            return MediaErrors.NotOwner;

        if (!media.IsReady)
            return MediaErrors.NotReady;

        // Union across posts AND messages: an upload is attachable exactly once, anywhere.
        var attached = await mediaRepository.GetAttachedIdsAsync([id], cancellationToken);
        return attached.Count > 0 ? MediaErrors.AlreadyAttached : null;
    }
}
