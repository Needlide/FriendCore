using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetUserPosts;

public sealed record GetUserPostsQuery(string Username, string? Cursor) : IRequest<ErrorOr<PostFeedResponse>>;
