using ErrorOr;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.SubmitAppeal;

/// <summary>A user appeals a moderation action taken against them (referenced by its audit-log id).</summary>
public sealed record SubmitAppealCommand(Ulid AuditLogId, string Reason) : IRequest<ErrorOr<Success>>;
