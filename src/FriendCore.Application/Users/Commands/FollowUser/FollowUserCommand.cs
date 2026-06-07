using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.FollowUser;

public sealed record FollowUserCommand(string Username) : IRequest<ErrorOr<Success>>;
