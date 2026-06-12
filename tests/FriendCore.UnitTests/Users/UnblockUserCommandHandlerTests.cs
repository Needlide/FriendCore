using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.UnblockUser;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class UnblockUserCommandHandlerTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IBlockRepository _blockRepository = Substitute.For<IBlockRepository>();
    private readonly Ulid _currentUserId = Ulid.NewUlid();
    private readonly User _target = UserTestFactory.Create(username: "bob");

    public UnblockUserCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _userRepository.GetByUsernameSlugIgnoringBlocksAsync("bob", Arg.Any<CancellationToken>()).Returns(_target);
    }

    private UnblockUserCommandHandler CreateHandler() =>
        new(_currentUserService, _userRepository, _blockRepository);

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await CreateHandler().Handle(new UnblockUserCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTargetMissing()
    {
        _userRepository.GetByUsernameSlugIgnoringBlocksAsync("ghost", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateHandler().Handle(new UnblockUserCommand("ghost"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Handle_RemovesOwnBlockRowOnly()
    {
        var result = await CreateHandler().Handle(new UnblockUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        await _blockRepository.Received(1).RemoveAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenNoBlockExists()
    {
        _blockRepository.RemoveAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await CreateHandler().Handle(new UnblockUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
    }
}
