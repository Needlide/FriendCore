using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetMyModerationActions;

/// <summary>The current user's own moderation actions, so they can appeal one (§13.3).</summary>
public sealed record GetMyModerationActionsQuery : IRequest<ErrorOr<MyModerationActionsResponse>>;
