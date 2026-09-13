using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<ErrorOr<Success>>;
