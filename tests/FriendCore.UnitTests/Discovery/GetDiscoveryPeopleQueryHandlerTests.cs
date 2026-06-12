using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Discovery.Queries.GetDiscoveryPeople;
using FriendCore.Domain.Discovery;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Discovery;

public class GetDiscoveryPeopleQueryHandlerTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDiscoveryRepository _discoveryRepository = Substitute.For<IDiscoveryRepository>();
    private readonly Ulid _userId = Ulid.NewUlid();

    public GetDiscoveryPeopleQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
    }

    private GetDiscoveryPeopleQueryHandler CreateHandler() =>
        new(_currentUserService, _discoveryRepository);

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new GetDiscoveryPeopleQuery(), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_MapsCandidatesInRepositoryOrder()
    {
        var first = UserTestFactory.Create(username: "first");
        var second = UserTestFactory.Create(username: "second");
        _discoveryRepository.GetPeopleAsync(_userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new DiscoveryCandidate(first, 13), new DiscoveryCandidate(second, 3)]);

        var result = await CreateHandler().Handle(new GetDiscoveryPeopleQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Collection(result.Value.People,
            person =>
            {
                Assert.Equal("first", person.Username);
                Assert.Equal(13, person.MatchScore);
                Assert.Equal(first.Id.ToString(), person.Id);
            },
            person =>
            {
                Assert.Equal("second", person.Username);
                Assert.Equal(3, person.MatchScore);
            });
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoCandidates()
    {
        _discoveryRepository.GetPeopleAsync(_userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await CreateHandler().Handle(new GetDiscoveryPeopleQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Empty(result.Value.People);
    }
}
