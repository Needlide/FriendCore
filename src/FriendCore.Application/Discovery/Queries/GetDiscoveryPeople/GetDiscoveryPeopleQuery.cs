using ErrorOr;
using FriendCore.Application.Common.Models;
using MediatR;

namespace FriendCore.Application.Discovery.Queries.GetDiscoveryPeople;

public sealed record GetDiscoveryPeopleQuery : IRequest<ErrorOr<DiscoveryPeopleResponse>>;
