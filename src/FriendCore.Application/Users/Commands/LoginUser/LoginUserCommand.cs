using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.LoginUser;

public sealed record LoginUserCommand(string Username, string Password) : IRequest<ErrorOr<AuthTokens>>;
