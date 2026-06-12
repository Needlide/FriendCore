using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Discovery.Queries.GetDiscoveryPosts;

public sealed record GetDiscoveryPostsQuery : IRequest<ErrorOr<DiscoveryPostsResponse>>;
