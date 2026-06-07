using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetFollowers;

public sealed record GetFollowersQuery(string Username) : IRequest<ErrorOr<FollowListResponse>>;
