using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Models;
using FriendCore.Application.Users.Commands.LoginUser;
using FriendCore.Domain.Users;
using NSubstitute;

namespace FriendCore.UnitTests.Users;

public class LoginUserCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 2, 9, 0, 0, TimeSpan.Zero);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    public LoginUserCommandHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(Now);
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>())
            .Returns(new AuthTokenResult("access-token", Now.AddHours(1)));
    }

    private LoginUserCommandHandler CreateHandler() =>
        new(_userRepository, _passwordHasher, _jwtTokenService, _dateTimeProvider);

    [Fact]
    public async Task Handle_ReturnsInvalidCredentials_WhenUserNotFound()
    {
        _userRepository.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateHandler().Handle(new LoginUserCommand("ghost@example.com", "pw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidCredentials.Code, result.FirstError.Code);
        _passwordHasher.DidNotReceive().Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ReturnsInvalidCredentials_WhenPasswordDoesNotMatch()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong", user.PasswordHash).Returns(false);

        var result = await CreateHandler().Handle(new LoginUserCommand(user.Email, "wrong"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.InvalidCredentials.Code, result.FirstError.Code);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsAccountBanned_WhenUserIsBanned()
    {
        var user = UserTestFactory.CreateBanned();
        _userRepository.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(true);

        var result = await CreateHandler().Handle(new LoginUserCommand(user.Email, "pw"), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(UserErrors.AccountBanned.Code, result.FirstError.Code);
        await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NormalizesEmailBeforeLookup()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(true);

        await CreateHandler().Handle(new LoginUserCommand("  Alice@Example.com  ", "pw"), CancellationToken.None);

        await _userRepository.Received(1).GetByEmailAsync(user.Email, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RecordsLoginAndReturnsAuthResponse_OnSuccess()
    {
        var user = UserTestFactory.Create();
        _userRepository.GetByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("correct", user.PasswordHash).Returns(true);

        var result = await CreateHandler().Handle(new LoginUserCommand(user.Email, "correct"), CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Equal("access-token", result.Value.AccessToken);
        Assert.Equal(user.Id.ToString(), result.Value.UserId);
        Assert.Equal(user.Username, result.Value.Username);
        Assert.Equal(user.DisplayName, result.Value.DisplayName);
        Assert.Equal(Now, user.LastActiveAt);
        await _userRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
    }
}
