using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.HideMessage;

internal sealed class HideMessageCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<HideMessageCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(HideMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // Same scoping as every other message operation: load the conversation first and refuse
        // anything the caller is not a participant of, so a guessed message id is a 404 rather
        // than a way to hide other people's messages.
        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        // §8.2: a block makes the conversation "not exist" for both sides, writes included.
        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        // A soft-deleted message is already out of the conversation for both sides, so it reads as
        // not-found here. That is free rather than special-cased: the query filter drops it, and
        // a user cannot see a deleted message to begin with.
        var message = await messagingRepository.GetMessageAsync(
            conversation.Id, request.MessageId, cancellationToken);
        if (message is null)
            return MessagingErrors.MessageNotFound;

        // Hiding your own message is the sender's call and means nothing to consent — that is the
        // existing Sender path, not this one. So this endpoint is only about somebody else's.
        // (It is a no-op either way, but the source below is what decides the refusal, so the
        // distinction has to be explicit rather than inferred.)
        var source = message.SenderId == userId
            ? MessageDeletionSource.Sender
            : MessageDeletionSource.Recipient;

        await messagingRepository.HideMessageAsync(
            message.Id, userId, dateTimeProvider.UtcNow, source, cancellationToken);

        // NO push. Not to the sender, not to the hider's other devices.
        //
        // To the sender: telling them their message was hidden leaks that the recipient acted on
        // it, and that reaction itself is not theirs to know. The protection does not need it —
        // the consent refusal recorded alongside is what actually stops the content returning,
        // and the sender will simply find their resend rejected with Media.RefusedByRecipient.
        //
        // To the hider's other devices: MessageReceived already never reaches your own sends, so
        // leaving them stale is the established behaviour for this app rather than a new gap.
        return Result.Success;
    }
}