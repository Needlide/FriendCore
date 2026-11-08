using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Commands.UpdateComment;

public sealed record UpdateCommentCommand(Ulid CommentId, string Content) : IRequest<ErrorOr<CommentResponse>>;
