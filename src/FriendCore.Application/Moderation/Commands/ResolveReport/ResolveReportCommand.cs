using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ResolveReport;

/// <summary>
///     A moderator resolves a report by choosing an enforcement action (or Dismiss). The reason
///     is mandatory (§10.3); SuspensionDays is required only for TemporarySuspension.
/// </summary>
public sealed record ResolveReportCommand(
    Ulid ReportId,
    ModerationActionInput Action,
    string Reason,
    int? SuspensionDays)
    : IRequest<ErrorOr<Success>>;
