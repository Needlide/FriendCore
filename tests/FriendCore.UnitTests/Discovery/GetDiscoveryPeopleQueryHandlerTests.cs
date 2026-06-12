using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Discovery.Queries.GetDiscoveryPeople;
using FriendCore.Domain.Discovery;
using FriendCore.Domain.Users;
using FriendCore.UnitTests.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Discovery;

public class GetDiscoveryPeopleQueryHandlerTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDiscoveryRepository _discoveryRepository = Substitute.For<IDiscoveryRepository>();
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IMuteRepository _muteRepository = Substitute.For<IMuteRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly Ulid _userId = Ulid.NewUlid();

    public GetDiscoveryPeopleQueryHandlerTests()
    {
        _currentUserService.UserId.Returns(_userId);
        _followRepository.GetFollowedIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);
        _muteRepository.GetMutedIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);
        _blockRepository.GetCounterpartIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);

        // Default cache behaviour: miss — invoke the factory (the live-query path).
        _cacheService.GetOrSetAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<List<DiscoveryPersonResponse>>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<List<DiscoveryPersonResponse>>>>()(
                CancellationToken.None));
    }

    private GetDiscoveryPeopleQueryHandler CreateHandler() =>
        new(_currentUserService, _discoveryRepository, _followRepository, _muteRepository, _blockRepository,
            _cacheService);

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
    public async Task Handle_ServesFromCache_WithoutQueryingTheRepository()
    {
        var cached = new List<DiscoveryPersonResponse>
        {
            new(Ulid.NewUlid().ToString(), "cached", "Cached", null, 10)
        };
        _cacheService.GetOrSetAsync(
                $"discovery:people:{_userId}",
                Arg.Any<Func<CancellationToken, Task<List<DiscoveryPersonResponse>>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(cached);

        var result = await CreateHandler().Handle(new GetDiscoveryPeopleQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("cached", Assert.Single(result.Value.People).Username);
        await _discoveryRepository.DidNotReceiveWithAnyArgs().GetPeopleAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_DropsStaleCachedEntries_ForCurrentExclusions()
    {
        // The cached list still contains users the viewer has since blocked, muted, or followed —
        // the serve-time re-check must drop all three without waiting for the TTL.
        var blockedId = Ulid.NewUlid();
        var mutedId = Ulid.NewUlid();
        var followedId = Ulid.NewUlid();
        var keptId = Ulid.NewUlid();
        var cached = new List<DiscoveryPersonResponse>
        {
            new(blockedId.ToString(), "blocked", "B", null, 13),
            new(mutedId.ToString(), "muted", "M", null, 10),
            new(followedId.ToString(), "followed", "F", null, 3),
            new(keptId.ToString(), "kept", "K", null, 3)
        };
        _cacheService.GetOrSetAsync(
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<List<DiscoveryPersonResponse>>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(cached);
        _blockRepository.GetCounterpartIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([blockedId]);
        _muteRepository.GetMutedIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([mutedId]);
        _followRepository.GetFollowedIdsAsync(_userId, Arg.Any<CancellationToken>()).Returns([followedId]);

        var result = await CreateHandler().Handle(new GetDiscoveryPeopleQuery(), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("kept", Assert.Single(result.Value.People).Username);
    }
}
