using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Posts.Queries.GetHomeFeed;

public sealed record GetHomeFeedQuery(string? Cursor) : IRequest<ErrorOr<PostFeedResponse>>;
