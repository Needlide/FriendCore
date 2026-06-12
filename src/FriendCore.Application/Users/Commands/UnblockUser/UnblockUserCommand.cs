using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.UnblockUser;

public sealed record UnblockUserCommand(string Username) : IRequest<ErrorOr<Success>>;
