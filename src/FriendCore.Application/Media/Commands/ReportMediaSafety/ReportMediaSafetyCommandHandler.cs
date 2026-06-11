using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Domain.Common;
using FriendCore.Domain.Media;
using MediatR;

namespace FriendCore.Application.Media.Commands.ReportMediaSafety;

internal sealed class ReportMediaSafetyCommandHandler(
    ICurrentUserService currentUserService,
    IMediaRepository mediaRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReportMediaSafetyCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        ReportMediaSafetyCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is null)
            return CommonErrors.Unauthorized;

        var media = await mediaRepository.GetByIdAsync(request.MediaId, cancellationToken);
        if (media is null)
            return MediaErrors.NotFound;

        // MVP: a single report flags immediately — warnings are additive and never cleared here,
        // and FlagSafety keeps a moderator decision authoritative over later reports. The full
        // report-queue/threshold flow is the Phase 6 moderation system.
        media.FlagSafety(
            request.Flashing, request.IntenseMotion, MediaSafetySource.UserReports, dateTimeProvider.UtcNow);
        await mediaRepository.UpdateAsync(cancellationToken);

        return Result.Success;
    }
}
