using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Media.Commands.DeleteMedia;
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
    ISender sender,
    MediaResponseFactory mediaResponseFactory,
    ReplyReferenceFactory replyReferenceFactory,
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

        // The reply target is validated before anything is written, and conversation-scoped, so
        // an id from another thread is indistinguishable from one that does not exist. A
        // soft-deleted target arrives null here because the global filter hides it — replying to a
        // message somebody has erased would leave a quote with nothing in it.
        if (request.ReplyToMessageId is { } replyToId
            && await messagingRepository.GetMessageAsync(conversation.Id, replyToId, cancellationToken) is null)
            return MessagingErrors.ReplyTargetNotFound;

        // Same gate the post path applies: every item must exist, belong to the sender, be
        // finished processing, and not already be spoken for. Reusing MediaItem wholesale
        // means DMs inherit the upload pipeline, variants, and safety flags unchanged.
        var mediaIds = request.MediaIds ?? [];
        if (mediaIds.Distinct().Count() != mediaIds.Count)
            return MessagingErrors.DuplicateMedia;

        var mediaValidation = await ValidateMediaAsync(mediaIds, userId, cancellationToken);
        if (mediaValidation.IsError)
            return mediaValidation.Errors;

        var mediaItems = mediaValidation.Value;

        // Consent check, after ownership but before anything is written: the recipient deleted
        // any of these exact images from this sender before, and that decision outranks a
        // re-upload. All-or-nothing, like the rest of the attachment set.
        if (mediaItems.Count > 0
            && conversation.OtherParticipantId(userId) is { } refusalRecipient)
        {
            var hashes = mediaItems
                .Select(media => media.ContentHash)
                .Where(hash => !string.IsNullOrEmpty(hash))
                .Select(hash => hash!)
                .Distinct()
                .ToList();

            var refused = await mediaRepository.GetRefusedHashesAsync(
                userId, refusalRecipient, hashes, cancellationToken);

            if (refused.Count > 0)
            {
                // Best-effort and per-image: only the refused uploads are deleted, so a sender
                // can remove the declined one and retry the rest without re-uploading them.
                var refusedSet = refused.ToHashSet(StringComparer.Ordinal);
                foreach (var media in mediaItems.Where(
                             media => media.ContentHash is { } hash && refusedSet.Contains(hash)))
                {
                    await TryDiscardRefusedUploadAsync(media.Id);
                }

                return MediaErrors.RefusedByRecipient;
            }
        }

        var messageResult = Message.Create(
            conversation.Id, userId, request.Content, now, mediaIds, request.ReplyToMessageId);
        if (messageResult.IsError)
            return messageResult.Errors;

        var message = messageResult.Value;
        conversation.RecordMessage(now);
        await messagingRepository.AddMessageAsync(message, cancellationToken);

        var mediaByMessage = await mediaResponseFactory.ForMessagesAsync([message], cancellationToken);
        // Resolved for the response because this record is also the MessageReceived payload: a
        // reply pushed live has to arrive with its quote, or the bubble would land looking like
        // the first word in the thread.
        var replyTo = await replyReferenceFactory.ForMessageAsync(message, cancellationToken);
        var response = MessageResponse.Create(
            message, mediaByMessage.GetValueOrDefault(message.Id), replyTo: replyTo);

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

    /// <summary>
    ///     Deletes an upload a refused send left behind. Best-effort by design: a refusal must
    ///     never fail because the cleanup did, and a leftover orphan is a storage cost rather
    ///     than a correctness problem.
    /// </summary>
    private async Task TryDiscardRefusedUploadAsync(Ulid mediaId)
    {
        try
        {
            await sender.Send(new DeleteMediaCommand(mediaId), CancellationToken.None);
        }
        catch (Exception)
        {
            // Intentionally swallowed — see above.
        }
    }

    // Mirrors CreatePost's media gate, so an item that failed there cannot slip through here.
    // Returns an empty list when there is no attachment to check.
    private async Task<ErrorOr<List<MediaItem>>> ValidateMediaAsync(
        IReadOnlyList<Ulid> mediaIds,
        Ulid userId,
        CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
            return new List<MediaItem>();

        var items = await mediaRepository.GetByIdsAsync(mediaIds, cancellationToken);

        // All-or-nothing: a partial match would silently send fewer images than requested.
        if (items.Count != mediaIds.Count)
            return MediaErrors.NotFound;

        if (items.Any(media => media.UploaderId != userId))
            return MediaErrors.NotOwner;

        if (items.Any(media => !media.IsReady))
            return MediaErrors.NotReady;

        // Union across posts AND messages: an upload is attachable exactly once, anywhere.
        var attached = await mediaRepository.GetAttachedIdsAsync(mediaIds, cancellationToken);
        if (attached.Count > 0)
            return MediaErrors.AlreadyAttached;

        return items.ToList();
    }
}
