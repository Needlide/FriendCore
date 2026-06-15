using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Moderation;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetModerationMetrics;

internal sealed class GetModerationMetricsQueryHandler(
    IReportRepository reportRepository,
    IAppealRepository appealRepository,
    IAdminAuditLogRepository auditLogRepository)
    : IRequestHandler<GetModerationMetricsQuery, ErrorOr<ModerationMetricsResponse>>
{
    public async Task<ErrorOr<ModerationMetricsResponse>> Handle(
        GetModerationMetricsQuery request,
        CancellationToken cancellationToken)
    {
        var reports = await reportRepository.GetMetricsAsync(cancellationToken);
        var appeals = await appealRepository.GetMetricsAsync(cancellationToken);
        var totalActions = await auditLogRepository.CountAsync(cancellationToken);

        var reviewed = appeals.Total - appeals.Pending;
        var overturnRate = reviewed == 0 ? 0d : (double)appeals.Overturned / reviewed;

        return new ModerationMetricsResponse(
            new ReportMetricsResponse(
                reports.Total, reports.Pending, reports.Resolved, reports.Dismissed, reports.AvgResolutionHours),
            new AppealMetricsResponse(
                appeals.Total, appeals.Pending, reviewed, appeals.Overturned, overturnRate, appeals.AvgResolutionHours),
            totalActions);
    }
}
