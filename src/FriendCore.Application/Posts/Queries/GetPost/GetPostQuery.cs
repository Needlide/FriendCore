using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPost;

public sealed record GetPostQuery(Ulid PostId) : IRequest<ErrorOr<PostResponse>>;
