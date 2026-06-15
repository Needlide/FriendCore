using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetAuditLog;

public sealed record GetAuditLogQuery : IRequest<ErrorOr<AuditLogResponse>>;
