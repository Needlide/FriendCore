using ErrorOr;
using MediatR;

namespace FriendCore.Application.Media.Commands.DeleteMedia;

public sealed record DeleteMediaCommand(Ulid MediaId) : IRequest<ErrorOr<Success>>;
