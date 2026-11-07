using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPostLikes;

public sealed record GetPostLikesQuery(Ulid PostId) : IRequest<ErrorOr<LikeListResponse>>;
