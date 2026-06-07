using ErrorOr;
using MediatR;

namespace FriendCore.Application.Posts.Commands.DeleteComment;

public sealed record DeleteCommentCommand(Ulid CommentId) : IRequest<ErrorOr<Success>>;
