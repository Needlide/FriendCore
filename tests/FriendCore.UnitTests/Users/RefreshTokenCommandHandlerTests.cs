using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.RefreshToken;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class RefreshTokenCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RefreshExpiresAt = Now.AddDays(60);
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    public RefreshTokenCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>())
            .Returns(new AuthTokenResult("access-token", Now.AddHours(1)));
    }

    private RefreshTokenCommandHandler CreateHandler()
    {
        return new RefreshTokenCommandHandler(
            _refreshTokenService, _userRepository, _jwtTokenService, _dateTimeProvider);
    }

    private void StubRotateSucceeds(Ulid userId)
    {
        _refreshTokenService.RotateAsync("raw", Arg.Any<CancellationToken>())
            .Returns(new RotatedRefreshToken(userId, "rotated-token", RefreshExpiresAt));
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenRotationFails()
    {
        _refreshTokenService.RotateAsync("raw", Arg.Any<CancellationToken>())
            .Returns(UserErrors.InvalidRefreshToken);

        var result = await CreateHandler().Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidRefreshToken.Code, result.FirstError.Code);
        // Rotation already revoked the presented token; no second blanket revoke is needed.
        await _refreshTokenService.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsInvalidRefreshToken_AndRevokesFamily_WhenUserMissing()
    {
        var userId = Ulid.NewUlid();
        StubRotateSucceeds(userId);
        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await CreateHandler().Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidRefreshToken.Code, result.FirstError.Code);
        await _refreshTokenService.Received(1).RevokeAllForUserAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsAccountBanned_AndRevokesFamily_WhenUserBanned()
    {
        var user = UserTestFactory.CreateBanned();
        StubRotateSucceeds(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.AccountBanned.Code, result.FirstError.Code);
        await _refreshTokenService.Received(1).RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsAccountSuspended_AndRevokesFamily_WhenUserSuspended()
    {
        var user = UserTestFactory.Create();
        user.Suspend(Now.AddDays(7), Now);
        StubRotateSucceeds(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.AccountSuspended.Code, result.FirstError.Code);
        await _refreshTokenService.Received(1).RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsFreshAccessTokenAndRotatedRefreshToken_OnSuccess()
    {
        var user = UserTestFactory.Create();
        StubRotateSucceeds(user.Id);
        _userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await CreateHandler().Handle(new RefreshTokenCommand("raw"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("access-token", result.Value.Auth.AccessToken);
        Assert.Equal(user.Id.ToString(), result.Value.Auth.UserId);
        Assert.Equal("rotated-token", result.Value.RefreshToken);
        Assert.Equal(RefreshExpiresAt, result.Value.RefreshTokenExpiresAt);
        await _refreshTokenService.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>());
    }
}
