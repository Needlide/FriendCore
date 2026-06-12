using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.MuteUser;

public sealed record MuteUserCommand(string Username) : IRequest<ErrorOr<Success>>;
