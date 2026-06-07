using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetPostComments;

public sealed record GetPostCommentsQuery(Ulid PostId, string? Cursor) : IRequest<ErrorOr<CommentFeedResponse>>;
