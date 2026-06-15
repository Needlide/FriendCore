using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetReportQueue;

/// <summary>The actionable (pending) report queue, priority-sorted. Moderator-gated at the route.</summary>
public sealed record GetReportQueueQuery : IRequest<ErrorOr<ReportQueueResponse>>;
