using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.UnfollowUser;

public sealed record UnfollowUserCommand(string Username) : IRequest<ErrorOr<Success>>;
