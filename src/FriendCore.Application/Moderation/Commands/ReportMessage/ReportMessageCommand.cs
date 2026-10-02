using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ReportMessage;

/// <param name="ConversationId">
/// Scopes the lookup so a message id from another conversation is indistinguishable from one
/// that does not exist, and so a conversation the reporter cannot access 404s as a whole.
/// </param>
public sealed record ReportMessageCommand(
    Ulid ConversationId,
    Ulid MessageId,
    ReportCategoryInput Category,
    string? Note)
    : IRequest<ErrorOr<Success>>;
