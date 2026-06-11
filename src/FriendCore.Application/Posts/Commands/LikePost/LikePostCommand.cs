using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Commands.LikePost;

public sealed record LikePostCommand(Ulid PostId) : IRequest<ErrorOr<PostLikeResponse>>;
