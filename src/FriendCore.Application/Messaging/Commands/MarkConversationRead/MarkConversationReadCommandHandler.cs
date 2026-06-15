using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.MarkConversationRead;

internal sealed class MarkConversationReadCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MarkConversationReadCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        MarkConversationReadCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        // Advances this participant's read marker to now; messages up to this point count as read.
        conversation.MarkRead(userId, dateTimeProvider.UtcNow);
        await messagingRepository.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
