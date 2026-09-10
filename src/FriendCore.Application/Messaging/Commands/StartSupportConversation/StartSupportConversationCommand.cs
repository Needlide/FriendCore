using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.StartSupportConversation;

/// <summary>
///     Find-or-create the caller's 1:1 conversation with the support inbox (oldest Admin).
///     The informal "chat with the team" channel — gratitude, bug reports, quick questions.
///     Distinct from the formal report/appeal flows (§13): no categories, no audit log.
/// </summary>
public sealed record StartSupportConversationCommand : IRequest<ErrorOr<ConversationResponse>>;
