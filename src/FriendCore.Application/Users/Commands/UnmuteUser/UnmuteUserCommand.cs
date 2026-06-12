using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.UnmuteUser;

public sealed record UnmuteUserCommand(string Username) : IRequest<ErrorOr<Success>>;
