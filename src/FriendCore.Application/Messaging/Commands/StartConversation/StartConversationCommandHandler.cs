using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.StartConversation;

internal sealed class StartConversationCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IBlockRepository blockRepository,
    IMessagingRepository messagingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<StartConversationCommand, ErrorOr<ConversationResponse>>
{
    public async Task<ErrorOr<ConversationResponse>> Handle(
        StartConversationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var slug = User.ToUsernameSlug(request.Username);
        var recipient = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (recipient is null || recipient.IsBanned)
            return MessagingErrors.RecipientNotFound;

        if (recipient.Id == userId)
            return MessagingErrors.CannotMessageSelf;

        // §8.1: blocked users cannot message. 404 (RecipientNotFound), not 403 — the recipient
        // lookup above is already block-filtered, but the explicit check also covers any future
        // unfiltered path and keeps the rule visible at the write site (§8.2).
        if (await blockRepository.ExistsBetweenAsync(userId, recipient.Id, cancellationToken))
            return MessagingErrors.RecipientNotFound;

        var conversation = await messagingRepository.FindDirectAsync(userId, recipient.Id, cancellationToken);
        if (conversation is null)
        {
            var created = Conversation.CreateDirect(userId, recipient.Id, dateTimeProvider.UtcNow);
            if (created.IsError)
                return created.Errors;

            // Returns the existing conversation if a simultaneous request won the race, so both
            // callers end up in the same thread instead of one of them getting a 500.
            conversation = await messagingRepository.AddConversationOrGetExistingAsync(
                created.Value, cancellationToken);
        }

        var unread = await messagingRepository.CountUnreadAsync(
            conversation.Id, userId, conversation.LastReadAtFor(userId), cancellationToken);

        return new ConversationResponse(
            conversation.Id.ToString(),
            UserSummaryResponse.FromUser(recipient),
            conversation.LastMessageAt,
            unread);
    }
}
