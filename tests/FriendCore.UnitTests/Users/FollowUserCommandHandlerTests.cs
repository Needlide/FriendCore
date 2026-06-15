using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Notifications.Events;
using FriendCore.Application.Users.Commands.FollowUser;
using FriendCore.Domain.Users;
using MediatR;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class FollowUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _currentUserId = Ulid.NewUlid();
    private readonly User _target = UserTestFactory.Create(username: "bob");

    public FollowUserCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByUsernameSlugAsync("bob", Arg.Any<CancellationToken>()).Returns(_target);
    }

    private FollowUserCommandHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _followRepository, _blockRepository, _publisher, _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new FollowUserCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTargetMissing()
    {
        _userRepository.GetByUsernameSlugAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new FollowUserCommand("ghost"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsCannotFollowSelf_WhenTargetIsCurrentUser()
    {
        _currentUserService.UserId.Returns(_target.Id);

        var result = await CreateHandler().Handle(new FollowUserCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.CannotFollowSelf", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_CreatesFollow_WhenNotAlreadyFollowing()
    {
        _followRepository.ExistsAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(new FollowUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        await _followRepository.Received(1).AddAsync(
            Arg.Is<Follow>(f => f.FollowerId == _currentUserId && f.FollowedId == _target.Id),
            Arg.Any<CancellationToken>());
        await _publisher.Received(1).Publish(
            Arg.Is<UserFollowedEvent>(e => e.FollowedId == _target.Id && e.FollowerId == _currentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenBlockExistsEitherDirection()
    {
        // 404, not 403 — a blocked user must not learn they were blocked (§8.2).
        _blockRepository.ExistsBetweenAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(new FollowUserCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
        await _followRepository.DidNotReceive().AddAsync(Arg.Any<Follow>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenAlreadyFollowing()
    {
        _followRepository.ExistsAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(new FollowUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        await _followRepository.DidNotReceive().AddAsync(Arg.Any<Follow>(), Arg.Any<CancellationToken>());
        // No new follow ⇒ no notification.
        await _publisher.DidNotReceiveWithAnyArgs().Publish(Arg.Any<UserFollowedEvent>(), Arg.Any<CancellationToken>());
    }
}
