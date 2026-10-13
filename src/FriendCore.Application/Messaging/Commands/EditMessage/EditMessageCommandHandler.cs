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
    ReplyReferenceFactory replyReferenceFactory,
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

        // Message.Edit owns the rules (length, empty text, attachment-only, stale version) and
        // reports why it refused.
        var edit = message.Edit(request.Content, dateTimeProvider.UtcNow, request.ExpectedVersion);
        if (edit.IsError)
            return edit.Errors;

        // The version compared above only covers what was true when this handler loaded the row.
        // This is the check that closes the window between that load and the write: one
        // statement, version in the WHERE clause, and it is what actually decides the winner.
        if (!await messagingRepository.TryApplyEditAsync(
                conversation.Id, message.Id, message.Content!, dateTimeProvider.UtcNow,
                request.ExpectedVersion ?? message.Version, cancellationToken))
            return MessagingErrors.MessageChanged;

        // Keep the in-memory entity consistent with what SQL just wrote. TryApplyEditAsync
        // detached it (an ExecuteUpdate is invisible to the tracker), and the response — which
        // is also the MessageEdited payload — has to carry the bumped version, or the other
        // participant's next edit of their copy would 409 against a version they never got.
        message.MarkEditCommitted();

        var mediaByMessage = await mediaResponseFactory.ForMessagesAsync([message], cancellationToken);

        // An edit does not touch reactions, but the edited message is about to be pushed whole
        // to both participants — so they have to travel with it. Omitting them would wipe every
        // chip on the recipient's screen on each keystroke-saved edit.
        var reactionsByMessage = await messagingRepository.GetReactionsAsync([message.Id], userId, cancellationToken);

        // The quote too. This is the easiest of the three paths to forget: editing a reply does
        // not change what it replies to, but the edit response IS the MessageEdited payload, so
        // omitting it would strip the quote off the recipient's bubble on every saved keystroke
        // while leaving the bubble itself intact.
        var replyTo = await replyReferenceFactory.ForMessageAsync(message, cancellationToken);

        var response = MessageResponse.Create(
            message,
            mediaByMessage.GetValueOrDefault(message.Id),
            ReactionResponse.FromTallies(reactionsByMessage.GetValueOrDefault(message.Id)),
            replyTo);

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