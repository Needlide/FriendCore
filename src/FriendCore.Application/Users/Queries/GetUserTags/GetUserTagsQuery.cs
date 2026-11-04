using ErrorOr;
using MediatR;
using FriendCore.Application.Common.Models;

namespace FriendCore.Application.Users.Queries.GetUserTags;

public sealed record GetUserTagsQuery(string Username) : IRequest<ErrorOr<UserTagsResponse>>;
