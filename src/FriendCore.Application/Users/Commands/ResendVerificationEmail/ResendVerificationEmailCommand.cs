using ErrorOr;
using MediatR;

namespace FriendCore.Application.Users.Commands.ResendVerificationEmail;

public sealed record ResendVerificationEmailCommand : IRequest<ErrorOr<Success>>;
