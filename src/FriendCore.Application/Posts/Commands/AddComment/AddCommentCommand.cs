using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Commands.AddComment;

public sealed record AddCommentCommand(Ulid PostId, string Content) : IRequest<ErrorOr<CommentResponse>>;
