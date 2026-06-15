using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetReport;

internal sealed class GetReportQueryHandler(
    IReportRepository reportRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetReportQuery, ErrorOr<ReportResponse>>
{
    public async Task<ErrorOr<ReportResponse>> Handle(GetReportQuery request, CancellationToken cancellationToken)
    {
        var report = await reportRepository.GetByIdAsync(request.ReportId, cancellationToken);
        if (report is null)
            return ReportErrors.NotFound;

        var reporter = (await userRepository.GetByIdsIgnoringBlocksAsync([report.ReporterId], cancellationToken))
            .FirstOrDefault();
        if (reporter is null)
            return ReportErrors.NotFound;

        return ReportResponse.Create(report, reporter);
    }
}
