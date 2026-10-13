using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Messaging.Commands.ToggleMessageReaction;

/// <summary>
///     Adds the viewer's reaction to <paramref name="Emoji" />, or removes it if they already
///     have it. One endpoint rather than an add/remove pair, because the action a user means is
///     "make this show my reaction" / "take mine back" — and tapping a highlighted chip again
///     to undo is the behaviour every messenger trains.
/// </summary>
/// <remarks>
///     A toggle is not idempotent the way the <c>LikePost</c> pair is: a duplicated request is
///     the opposite of a no-op. The response carries the resulting state, so a client that
///   double-fired still converges on the truth, and a dropped request costs a tap rather than
///   flipping the reaction back.
/// </remarks>
public sealed record ToggleMessageReactionCommand(
    Ulid ConversationId,
    Ulid MessageId,
    string Emoji) : IRequest<ErrorOr<ReactionResponse>>;