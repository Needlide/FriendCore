using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.SearchUsers;

/// <summary>§3/§7 MVP: global people search by username or display name.</summary>
public sealed record SearchUsersQuery(string Query) : IRequest<ErrorOr<UserSearchResponse>>;
