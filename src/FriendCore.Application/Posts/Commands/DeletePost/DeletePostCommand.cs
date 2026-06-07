using ErrorOr;
using MediatR;

namespace FriendCore.Application.Posts.Commands.DeletePost;

public sealed record DeletePostCommand(Ulid PostId) : IRequest<ErrorOr<Success>>;
