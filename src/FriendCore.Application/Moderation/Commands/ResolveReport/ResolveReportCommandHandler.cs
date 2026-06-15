using ErrorOr;
using FriendCore.Application.Moderation.Common;
using FriendCore.Application.Moderation.Services;
using FriendCore.Domain.Common;
using FriendCore.Domain.Moderation;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ResolveReport;

internal sealed class ResolveReportCommandHandler(
    IReportRepository reportRepository,
    IModerationService moderationService)
    : IRequestHandler<ResolveReportCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ResolveReportCommand request, CancellationToken cancellationToken)
    {
        var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report is null)
            return ReportErrors.NotFound;

        return await moderationService.ExecuteAsync(
            request.Action.ToDomain(),
            report.TargetType,
            report.TargetId,
            request.Reason,
            request.SuspensionDays,
            report,
            cancellationToken);
    }
}
