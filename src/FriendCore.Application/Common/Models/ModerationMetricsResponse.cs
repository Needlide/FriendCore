namespace FriendCore.Application.Common.Models;

public sealed record ReportMetricsResponse(
    int Total,
    int Pending,
    int Resolved,
    int Dismissed,
    double? AvgResolutionHours);

public sealed record AppealMetricsResponse(
    int Total,
    int Pending,
    int Reviewed,
    int Overturned,
    double OverturnRate,
    double? AvgResolutionHours);

public sealed record ModerationMetricsResponse(
    ReportMetricsResponse Reports,
    AppealMetricsResponse Appeals,
    int TotalModerationActions);
