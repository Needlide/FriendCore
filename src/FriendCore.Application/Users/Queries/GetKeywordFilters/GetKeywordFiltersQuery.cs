using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Users.Queries.GetKeywordFilters;

public sealed record GetKeywordFiltersQuery : IRequest<ErrorOr<KeywordFiltersResponse>>;
