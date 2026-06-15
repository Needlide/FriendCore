using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Common;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Posts;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportPost;

internal sealed class ReportPostCommandHandler(
    ICurrentUserService currentUserService,
    IPostRepository postRepository,
    IReportRepository reportRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReportPostCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ReportPostCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        // Block-filtered lookup: a post you can't see (deleted or block-hidden) can't be reported.
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
            return PostErrors.NotFound;

        // Idempotent: one pending report per reporter per target — silently no-op on a duplicate.
        if (await reportRepository.PendingExistsAsync(userId, ReportTargetType.Post, post.Id, cancellationToken))
            return Result.Success;

        var report = Report.Create(
            userId, ReportTargetType.Post, post.Id, request.Category.ToDomain(), request.Note,
            dateTimeProvider.UtcNow);
        if (report.IsError)
            return report.Errors;

        await reportRepository.AddAsync(report.Value, cancellationToken);
        return Result.Success;
    }
}
