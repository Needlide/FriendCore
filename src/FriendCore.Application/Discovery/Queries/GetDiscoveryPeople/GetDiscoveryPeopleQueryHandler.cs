using ErrorOr;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Domain.Common;
using FriendCore.Domain.Discovery;
using MediatR;

namespace FriendCore.Application.Discovery.Queries.GetDiscoveryPeople;

internal sealed class GetDiscoveryPeopleQueryHandler(
    ICurrentUserService currentUserService,
    IDiscoveryRepository discoveryRepository)
    : IRequestHandler<GetDiscoveryPeopleQuery, ErrorOr<DiscoveryPeopleResponse>>
{
    private const int MaxResults = 20;

    public async Task<ErrorOr<DiscoveryPeopleResponse>> Handle(
        GetDiscoveryPeopleQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
            return CommonErrors.Unauthorized;

        var candidates = await discoveryRepository.GetPeopleAsync(userId, MaxResults, cancellationToken);

        var people = candidates
            .Select(candidate => new DiscoveryPersonResponse(
                candidate.User.Id.ToString(),
                candidate.User.Username,
                candidate.User.DisplayName,
                candidate.User.Profile.Bio,
                candidate.Score))
            .ToList();

        return new DiscoveryPeopleResponse(people);
    }
}
