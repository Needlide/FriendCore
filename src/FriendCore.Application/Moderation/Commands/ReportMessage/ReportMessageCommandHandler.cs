using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Messaging;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportMessage;

internal sealed class ReportMessageCommandHandler(
    ICurrentUserService currentUserService,
    IMessagingRepository messagingRepository,
    IBlockRepository blockRepository,
    IReportRepository reportRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReportMessageCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ReportMessageCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var message = await messagingRepository.GetMessageAsync(
            request.ConversationId, request.MessageId, cancellationToken);
        if (message is null)
            return MessagingErrors.MessageNotFound;

        // Before block filtering is considered: reporting your own message is never meaningful,
        // and it would otherwise let you fill your own queue with noise.
        if (message.SenderId == userId)
            return ReportErrors.CannotReportSelf;

        var conversation = await messagingRepository.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null || !conversation.HasParticipant(userId))
            return MessagingErrors.ConversationNotFound;

        // §8.2: you cannot report into a conversation you no longer have access to. Checked after
        // the message lookup so the reply does not distinguish "no such message" from "not yours".
        if (conversation.OtherParticipantId(userId) is { } otherId
            && await blockRepository.ExistsBetweenAsync(userId, otherId, cancellationToken))
            return MessagingErrors.ConversationNotFound;

        // Idempotent, like post and user reports: one pending report per reporter per target.
        if (await reportRepository.PendingExistsAsync(
                userId, ReportTargetType.Message, message.Id, cancellationToken))
            return Result.Success;

        var report = Report.Create(
            userId, ReportTargetType.Message, message.Id, request.Category.ToDomain(), request.Note,
            dateTimeProvider.UtcNow);
        if (report.IsError)
            return report.Errors;

        await reportRepository.AddAsync(report.Value, cancellationToken);
        return Result.Success;
    }
}