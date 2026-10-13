using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Common;
using FriendCore.Application.Moderation.Services;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportUser;

internal sealed class ReportUserCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IReportRepository reportRepository,
    ISubjectSnapshotFactory snapshotFactory,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReportUserCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ReportUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var slug = User.ToUsernameSlug(request.Username);
        var target = await userRepository.GetByUsernameSlugAsync(slug, cancellationToken);
        if (target is null || target.IsBanned)
            return UserErrors.NotFound;

        if (target.Id == userId)
            return ReportErrors.CannotReportSelf;

        if (await reportRepository.PendingExistsAsync(userId, ReportTargetType.User, target.Id, cancellationToken))
            return Result.Success;

        // Captured here, while the subject still exists — at review time it may be

        // gone, which is the whole problem this snapshot exists to solve.

        var snapshot = await snapshotFactory.CaptureAsync(

            ReportTargetType.User, target.Id, cancellationToken);

        var report = Report.Create(
            userId, ReportTargetType.User, target.Id, request.Category.ToDomain(), request.Note,
            dateTimeProvider.UtcNow, snapshot);
        if (report.IsError)
            return report.Errors;

        await reportRepository.AddAsync(report.Value, cancellationToken);
        return Result.Success;
    }
}
