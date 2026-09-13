using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetCommand(string Username, string Email) : IRequest<ErrorOr<Success>>;
