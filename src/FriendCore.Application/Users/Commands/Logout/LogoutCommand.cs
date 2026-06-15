using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.Logout;

/// <summary>Revokes the presented refresh token. Idempotent — succeeds even if it's already gone.</summary>
public sealed record LogoutCommand(string RefreshToken) : IRequest<ErrorOr<Success>>;
