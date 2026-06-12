using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.BlockUser;

/// <summary>
///     <paramref name="Confirmed" /> drives the server-side confirmation flow: when a follow
///     relationship exists in either direction, an unconfirmed block is rejected with
///     <c>User.BlockRequiresConfirmation</c> so the client can ask the user before severing it.
/// </summary>
public sealed record BlockUserCommand(string Username, bool Confirmed) : IRequest<ErrorOr<Success>>;
