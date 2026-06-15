using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetModerationMetrics;

public sealed record GetModerationMetricsQuery : IRequest<ErrorOr<ModerationMetricsResponse>>;
