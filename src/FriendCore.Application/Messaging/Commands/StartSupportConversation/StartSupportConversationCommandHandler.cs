using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.StartSupportConversation;

internal sealed class StartSupportConversationCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IMessagingRepository messagingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<StartSupportConversationCommand, ErrorOr<ConversationResponse>>
{
    public async Task<ErrorOr<ConversationResponse>> Handle(
        StartSupportConversationCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // Oldest admin staffs the MVP inbox. Block-ignoring by design: the help channel must
        // stay reachable even across a (legacy) block, and blocking the admin is rejected in
        // BlockUser going forward so no new admin blocks can appear.
        var admin = await userRepository.GetFirstAdminAsync(cancellationToken);
        if (admin is null || admin.IsBanned)
            return MessagingErrors.SupportUnavailable;

        if (admin.Id == userId)
            return MessagingErrors.CannotMessageSelf;

        // No block check, no email-verification gate: an unverified or restricted user asking
        // "why can't I verify / post?" is exactly who this channel is for.
        var conversation = await messagingRepository.FindDirectAsync(userId, admin.Id, cancellationToken);
        if (conversation is null)
        {
            var created = Conversation.CreateDirect(userId, admin.Id, dateTimeProvider.UtcNow);
            if (created.IsError)
                return created.Errors;

            conversation = created.Value;
            await messagingRepository.AddConversationAsync(conversation, cancellationToken);
        }

        var unread = await messagingRepository.CountUnreadAsync(
            conversation.Id, userId, conversation.LastReadAtFor(userId), cancellationToken);

        return new ConversationResponse(
            conversation.Id.ToString(),
            UserSummaryResponse.FromUser(admin),
            conversation.LastMessageAt,
            unread);
    }
}
