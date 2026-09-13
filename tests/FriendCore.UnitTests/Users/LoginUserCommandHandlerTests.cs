using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Users.Commands.LoginUser;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class LoginUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    public LoginUserCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>())
            .Returns(new AuthTokenResult("access-token", Now.AddHours(1)));
        _refreshTokenService.IssueAsync(Arg.Any<Ulid>(), Arg.Any<CancellationToken>())
            .Returns(new IssuedRefreshToken("refresh-token", Now.AddDays(60)));
    }

    private LoginUserCommandHandler CreateHandler()
    {
        return new LoginUserCommandHandler(
            _userRepository, _passwordHasher, _jwtTokenService, _refreshTokenService, _dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ReturnsInvalidCredentials_WhenUserNotFound()
    {
        _userRepository.GetByUsernameSlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateHandler()
            .Handle(new LoginUserCommand("ghost", "pw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidCredentials.Code, result.FirstError.Code);
        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ReturnsInvalidCredentials_WhenPasswordDoesNotMatch()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByUsernameSlugAsync(user.UsernameSlug, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong", user.PasswordHash).Returns(false);

        var result = await CreateHandler().Handle(new LoginUserCommand(user.Username, "wrong"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidCredentials.Code, result.FirstError.Code);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsAccountBanned_WhenUserIsBanned()
    {
        var user = UserTestFactory.CreateBanned();
        _userRepository.GetByUsernameSlugAsync(user.UsernameSlug, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(true);

        var result = await CreateHandler().Handle(new LoginUserCommand(user.Username, "pw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.AccountBanned.Code, result.FirstError.Code);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NormalizesUsernameBeforeLookup()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByUsernameSlugAsync(user.UsernameSlug, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(true);

        await CreateHandler().Handle(new LoginUserCommand("  Alice  ", "pw"), CancellationToken.None);

        await _userRepository.Received(1).GetByUsernameSlugAsync(user.UsernameSlug, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RecordsLoginAndReturnsAuthResponse_OnSuccess()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByUsernameSlugAsync(user.UsernameSlug, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("correct", user.PasswordHash).Returns(true);

        var result = await CreateHandler().Handle(new LoginUserCommand(user.Username, "correct"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("access-token", result.Value.Auth.AccessToken);
        Assert.Equal("refresh-token", result.Value.RefreshToken);
        Assert.Equal(user.Id.ToString(), result.Value.Auth.UserId);
        Assert.Equal(user.Username, result.Value.Auth.Username);
        Assert.Equal(user.DisplayName, result.Value.Auth.DisplayName);
        Assert.Equal(Now, user.LastActiveAt);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }
}
