using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Moderation.Queries.GetUserAccount;

public sealed record GetUserAccountQuery(string Username) : IRequest<ErrorOr<UserAccountResponse>>;
