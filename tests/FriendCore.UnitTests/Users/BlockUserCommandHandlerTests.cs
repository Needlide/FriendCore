using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.BlockUser;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class BlockUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly IFollowRepository _followRepository = Substitute.For<IFollowRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _currentUserId = Ulid.NewUlid();
    private readonly User _target = UserTestFactory.Create(username: "bob");

    public BlockUserCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByUsernameSlugAsync("bob", Arg.Any<CancellationToken>()).Returns(_target);
    }

    private BlockUserCommandHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _blockRepository, _followRepository, _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new BlockUserCommand("bob", false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTargetMissing()
    {
        _userRepository.GetByUsernameSlugAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new BlockUserCommand("ghost", false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsCannotBlockSelf_WhenTargetIsCurrentUser()
    {
        _currentUserService.UserId.Returns(_target.Id);

        var result = await CreateHandler().Handle(new BlockUserCommand("bob", false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.CannotBlockSelf", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_BlocksImmediately_WhenNoFollowRelationship()
    {
        _followRepository.ExistsBetweenAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateHandler().Handle(new BlockUserCommand("bob", false), CancellationToken.None);

        Assert.False(result.IsError);
        await _blockRepository.Received(1).AddAndRemoveFollowsAsync(
            Arg.Is<Block>(b => b.BlockerId == _currentUserId && b.BlockedId == _target.Id && b.CreatedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequiresConfirmation_WhenFollowRelationshipExists()
    {
        _followRepository.ExistsBetweenAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(new BlockUserCommand("bob", false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.BlockRequiresConfirmation", result.FirstError.Code);
        await _blockRepository.DidNotReceiveWithAnyArgs().AddAndRemoveFollowsAsync(default!, default);
    }

    [Fact]
    public async Task Handle_Blocks_WhenFollowRelationshipExistsAndConfirmed()
    {
        _followRepository.ExistsBetweenAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(new BlockUserCommand("bob", true), CancellationToken.None);

        Assert.False(result.IsError);
        await _blockRepository.Received(1).AddAndRemoveFollowsAsync(
            Arg.Is<Block>(b => b.BlockerId == _currentUserId && b.BlockedId == _target.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenAlreadyBlocked()
    {
        _blockRepository.ExistsAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await CreateHandler().Handle(new BlockUserCommand("bob", false), CancellationToken.None);

        Assert.False(result.IsError);
        await _blockRepository.DidNotReceiveWithAnyArgs().AddAndRemoveFollowsAsync(default!, default);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTargetBanned()
    {
        var banned = UserTestFactory.CreateBanned();
        _userRepository.GetByUsernameSlugAsync(banned.UsernameSlug, Arg.Any<CancellationToken>()).Returns(banned);

        var result = await CreateHandler().Handle(
            new BlockUserCommand(banned.Username, false), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }
}
