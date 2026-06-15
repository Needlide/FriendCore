using ErrorOr;
using FriendCore.Application.Moderation.Common;
using MediatR;

namespace FriendCore.Application.Moderation.Commands.ActOnUser;

/// <summary>Direct enforcement against a user from the §13.5 account view (no originating report).</summary>
public sealed record ActOnUserCommand(
    string Username,
    ModerationActionInput Action,
    string Reason,
    int? SuspensionDays)
    : IRequest<ErrorOr<Success>>;
