using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Moderation.Common;
using FriendCore.Application.Moderation.Services;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using FriendCore.Domain.Moderation;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportMedia;

internal sealed class ReportMediaCommandHandler(
    ICurrentUserService currentUserService,
    IMediaRepository mediaRepository,
    IReportRepository reportRepository,
    ISubjectSnapshotFactory snapshotFactory,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReportMediaCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ReportMediaCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var media = await mediaRepository.GetByIdAsync(request.MediaId, cancellationToken);
        if (media is null)
            return MediaErrors.NotFound;

        if (media.UploaderId == userId)
            return ReportErrors.CannotReportSelf;

        // Idempotent, like post and user reports: one pending report per reporter per target.
        if (await reportRepository.PendingExistsAsync(
                userId, ReportTargetType.Media, media.Id, cancellationToken))
            return Result.Success;

        // Gate the media for opted-in readers IMMEDIATELY, in the same operation that files the
        // report. Filing a report a moderator may not see for hours would otherwise leave
        // harmful media in front of exactly the people who asked to be shielded from it — and
        // FlagSafety keeps a moderator's later decision authoritative over this one.
        //
        // Only for the flashing/intense-motion category: a copyright or nudity report is not
        // evidence that the image flickers, and flagging it as though it did would be a lie in
        // the safety data. This is also why motion shares that category — it is the one bucket
        // whose report means "gate this now".
        if (request.Category == ReportCategoryInput.FlashingLights)
            media.FlagSafety(
                MediaSafetyFlags.From(("flashing", true), ("intenseMotion", true)),
                MediaSafetySource.UserReports,
                dateTimeProvider.UtcNow);

        // Captured here, while the subject still exists — at review time it may be

        // gone, which is the whole problem this snapshot exists to solve.

        var snapshot = await snapshotFactory.CaptureAsync(

            ReportTargetType.Media, media.Id, cancellationToken);

        var report = Report.Create(
            userId, ReportTargetType.Media, media.Id, request.Category.ToDomain(), request.Note,
            dateTimeProvider.UtcNow, snapshot);
        if (report.IsError)
            return report.Errors;

        // One SaveChanges: the gate and the report row commit together, so a queued report can
        // never exist without the media actually being gated (or vice versa).
        await reportRepository.AddAsync(report.Value, cancellationToken);
        return Result.Success;
    }
}
