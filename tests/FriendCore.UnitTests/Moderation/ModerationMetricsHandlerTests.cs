using FriendCore.Application.Moderation.Queries.GetModerationMetrics;
using FriendCore.Domain.Moderation;
using NSubstitute;

namespace FriendCore.UnitTests.Moderation;

public class ModerationMetricsHandlerTests
{
    private readonly IReportRepository _reportRepository = Substitute.For<IReportRepository>();
    private readonly IAppealRepository _appealRepository = Substitute.For<IAppealRepository>();
    private readonly IAdminAuditLogRepository _auditLogRepository = Substitute.For<IAdminAuditLogRepository>();

    private GetModerationMetricsQueryHandler CreateHandler() =>
        new(_reportRepository, _appealRepository, _auditLogRepository);

    [Fact]
    public async Task Handle_ComputesReviewedCount_AndOverturnRate()
    {
        _reportRepository.GetMetricsAsync(Arg.Any<CancellationToken>())
            .Returns(new ReportMetrics(10, 3, 5, 2, 4.5));
        _appealRepository.GetMetricsAsync(Arg.Any<CancellationToken>())
            .Returns(new AppealMetrics(4, 1, 2, 12.0)); // 3 reviewed, 2 overturned
        _auditLogRepository.CountAsync(Arg.Any<CancellationToken>()).Returns(7);

        var result = await CreateHandler().Handle(new GetModerationMetricsQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(10, result.Value.Reports.Total);
        Assert.Equal(4.5, result.Value.Reports.AvgResolutionHours);
        Assert.Equal(3, result.Value.Appeals.Reviewed);
        Assert.Equal(2, result.Value.Appeals.Overturned);
        Assert.Equal(2d / 3d, result.Value.Appeals.OverturnRate, 5);
        Assert.Equal(7, result.Value.TotalModerationActions);
    }

    [Fact]
    public async Task Handle_OverturnRateIsZero_WhenNothingReviewed()
    {
        _reportRepository.GetMetricsAsync(Arg.Any<CancellationToken>())
            .Returns(new ReportMetrics(0, 0, 0, 0, null));
        _appealRepository.GetMetricsAsync(Arg.Any<CancellationToken>())
            .Returns(new AppealMetrics(2, 2, 0, null)); // all pending
        _auditLogRepository.CountAsync(Arg.Any<CancellationToken>()).Returns(0);

        var result = await CreateHandler().Handle(new GetModerationMetricsQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal(0, result.Value.Appeals.Reviewed);
        Assert.Equal(0d, result.Value.Appeals.OverturnRate);
        Assert.Null(result.Value.Reports.AvgResolutionHours);
    }
}
