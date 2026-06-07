using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetFollowing;

public sealed record GetFollowingQuery(string Username) : IRequest<ErrorOr<FollowListResponse>>;
