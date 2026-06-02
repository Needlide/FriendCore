using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Username,
    string DisplayName,
    string Email,
    string Password) : IRequest<ErrorOr<AuthResponse>>;