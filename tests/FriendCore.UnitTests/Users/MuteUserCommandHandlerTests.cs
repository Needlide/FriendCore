using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.MuteUser;
using FriendCore.Application.Users.Commands.UnmuteUser;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class MuteUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IMuteRepository _muteRepository = Substitute.For<IMuteRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly Ulid _currentUserId = Ulid.NewUlid();
    private readonly User _target = UserTestFactory.Create(username: "bob");

    public MuteUserCommandHandlerTests()
    {
        _currentUserService.UserId.Returns(_currentUserId);
        _dateTimeProvider.UtcNow.Returns(Now);
        _userRepository.GetByUsernameSlugAsync("bob", Arg.Any<CancellationToken>()).Returns(_target);
    }

    private MuteUserCommandHandler MuteHandler() =>
        new(_currentUserService, _userRepository, _muteRepository, _dateTimeProvider);

    private UnmuteUserCommandHandler UnmuteHandler() =>
        new(_currentUserService, _userRepository, _muteRepository);

    [Fact]
    public async Task Mute_ReturnsUnauthorized_WhenNoCurrentUser()
    {
        _currentUserService.UserId.Returns((Ulid?)null);

        var result = await MuteHandler().Handle(new MuteUserCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("Common.Unauthorized", result.FirstError.Code);
    }

    [Fact]
    public async Task Mute_ReturnsNotFound_WhenTargetMissing()
    {
        _userRepository.GetByUsernameSlugAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await MuteHandler().Handle(new MuteUserCommand("ghost"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Mute_ReturnsNotFound_WhenTargetBanned()
    {
        var banned = UserTestFactory.CreateBanned();
        _userRepository.GetByUsernameSlugAsync(banned.UsernameSlug, Arg.Any<CancellationToken>()).Returns(banned);

        var result = await MuteHandler().Handle(new MuteUserCommand(banned.Username), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }

    [Fact]
    public async Task Mute_ReturnsCannotMuteSelf_WhenTargetIsCurrentUser()
    {
        _currentUserService.UserId.Returns(_target.Id);

        var result = await MuteHandler().Handle(new MuteUserCommand("bob"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.CannotMuteSelf", result.FirstError.Code);
    }

    [Fact]
    public async Task Mute_CreatesMute_WhenNotAlreadyMuted()
    {
        _muteRepository.ExistsAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await MuteHandler().Handle(new MuteUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        await _muteRepository.Received(1).AddAsync(
            Arg.Is<Mute>(m => m.MuterId == _currentUserId && m.MutedId == _target.Id && m.CreatedAt == Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mute_IsIdempotent_WhenAlreadyMuted()
    {
        _muteRepository.ExistsAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>()).Returns(true);

        var result = await MuteHandler().Handle(new MuteUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        await _muteRepository.DidNotReceive().AddAsync(Arg.Any<Mute>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unmute_RemovesOwnMuteRow()
    {
        var result = await UnmuteHandler().Handle(new UnmuteUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
        await _muteRepository.Received(1).RemoveAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unmute_IsIdempotent_WhenNoMuteExists()
    {
        _muteRepository.RemoveAsync(_currentUserId, _target.Id, Arg.Any<CancellationToken>()).Returns(false);

        var result = await UnmuteHandler().Handle(new UnmuteUserCommand("bob"), CancellationToken.None);

        Assert.False(result.IsError);
    }

    [Fact]
    public async Task Unmute_ReturnsNotFound_WhenTargetMissing()
    {
        _userRepository.GetByUsernameSlugAsync("ghost", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await UnmuteHandler().Handle(new UnmuteUserCommand("ghost"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("User.NotFound", result.FirstError.Code);
    }
}
