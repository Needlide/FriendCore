namespace FriendCore.Domain.Moderation;

// Aggregates for the §13.5 metrics dashboard. AvgResolutionHours is null when nothing has been
// resolved yet.
public sealed record ReportMetrics(
    int Total,
    int Pending,
    int Resolved,
    int Dismissed,
    double? AvgResolutionHours);

public sealed record AppealMetrics(
    int Total,
    int Pending,
    int Overturned,
    double? AvgResolutionHours);
