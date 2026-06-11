using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Commands.UnlikePost;

public sealed record UnlikePostCommand(Ulid PostId) : IRequest<ErrorOr<PostLikeResponse>>;
