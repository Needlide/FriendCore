using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetReport;

public sealed record GetReportQuery(Ulid ReportId) : IRequest<ErrorOr<ReportResponse>>;
