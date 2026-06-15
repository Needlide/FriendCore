using ErrorOr;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Moderation;
using FriendCore.Domain.Users;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetReportQueue;

internal sealed class GetReportQueueQueryHandler(
    IReportRepository reportRepository,
    IUserRepository userRepository)
    : IRequestHandler<GetReportQueueQuery, ErrorOr<ReportQueueResponse>>
{
    private const int MaxResults = 100;

    public async Task<ErrorOr<ReportQueueResponse>> Handle(
        GetReportQueueQuery request,
        CancellationToken cancellationToken)
    {
        var reports = await reportRepository.GetQueueAsync(ReportStatus.Pending, MaxResults, cancellationToken);
        if (reports.Count == 0)
            return new ReportQueueResponse([]);

        // Block-ignoring: the moderator must see every reporter regardless of their own blocks.
        var reporters = (await userRepository.GetByIdsIgnoringBlocksAsync(
                reports.Select(report => report.ReporterId).Distinct().ToList(), cancellationToken))
            .ToDictionary(user => user.Id);

        var items = reports
            .Where(report => reporters.ContainsKey(report.ReporterId))
            .Select(report => ReportResponse.Create(report, reporters[report.ReporterId]))
            .ToList();

        return new ReportQueueResponse(items);
    }
}
