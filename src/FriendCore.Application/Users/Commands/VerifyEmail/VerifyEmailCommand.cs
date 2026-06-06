using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(string Token) : IRequest<ErrorOr<AuthResponse>>;
